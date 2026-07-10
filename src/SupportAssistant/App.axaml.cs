using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using InferenceEngine.Core.Text;
using SupportAssistant.Core.Agent;
using SupportAssistant.Core.Engines;
using SupportAssistant.Core.Security;
using SupportAssistant.Core.Services;
using SupportAssistant.Core.Tools;
using SupportAssistant.Security;
using SupportAssistant.ViewModels;
using SupportAssistant.Views;

namespace SupportAssistant;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Check if this is the first run
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            
            if (settingsService.Settings.General.IsFirstRun)
            {
                // Show onboarding wizard
                var onboardingWindow = new OnboardingWizardWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<OnboardingWizardViewModel>(),
                };
                
                desktop.MainWindow = onboardingWindow;
                
                // Handle wizard completion
                onboardingWindow.Closing += (sender, e) =>
                {
                    var vm = (OnboardingWizardViewModel?)onboardingWindow.DataContext;
                    if (vm?.IsCompleted == true)
                    {
                        // Show main window
                        var mainWindow = new MainWindow
                        {
                            DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
                        };
                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        
                        // Start background initialization.
                        // ReactiveCommand.Execute() returns a cold IObservable<Unit> that must be
                        // subscribed to actually run the command; simply discarding it (fire-and-forget
                        // assignment) would never start initialization.
                        var backgroundTaskVm = ((MainWindowViewModel)mainWindow.DataContext).BackgroundTask;
                        backgroundTaskVm.StartInitializationCommand.Execute().Subscribe();
                    }
                };
            }
            else
            {
                // Show main window directly
                desktop.MainWindow = new MainWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Settings service (should be one of the first)
        services.AddSingleton<ISettingsService, SettingsService>();
        
        // Core services
        services.AddSingleton<IOnnxRuntimeService, OnnxRuntimeService>();
        services.AddSingleton<IConfigurationService, DefaultConfigurationService>();

        // Inference diagnostics (WS1): a library-decoupled snapshot store fed by the engines'
        // OnSessionInitialized callback. Registered before the engine factories so the factories
        // can resolve it.
        services.AddSingleton<InferenceDiagnosticsService>();
        services.AddSingleton<IInferenceDiagnosticsService>(sp => sp.GetRequiredService<InferenceDiagnosticsService>());

        // Inference engines (Phase 4 Stage 1). The library fetches models + tokenizer assets lazily
        // on first use; options are derived from the user's GPU/execution-provider setting. The
        // diagnostics service is attached so the resolved provider + fallback trail is surfaced.
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            var diag = sp.GetRequiredService<InferenceDiagnosticsService>();
            var options = InferenceOptionsFactory.Create(settings, diag, engineKind: "Embedding");
            return new TextEmbeddingEngine(options);
        });
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            var diag = sp.GetRequiredService<InferenceDiagnosticsService>();
            var options = InferenceOptionsFactory.Create(settings, diag, engineKind: "Generation");
            return new TextGenerationEngine(options);
        });

        // Embedding service (adapter over the embedding engine; falls back to simple embeddings).
        services.AddSingleton<IEmbeddingServiceFactory>(sp => new EmbeddingServiceFactory(
            sp.GetRequiredService<TextEmbeddingEngine>(),
            sp.GetRequiredService<IConfigurationService>()));
        services.AddSingleton<IEmbeddingService>(sp =>
        {
            var factory = sp.GetRequiredService<IEmbeddingServiceFactory>();
            // Resolve off the current (potentially UI) thread to avoid a sync-over-async deadlock.
            return Task.Run(() => factory.CreateEmbeddingServiceAsync()).GetAwaiter().GetResult();
        });

        // Language-model service (adapter over the generation engine).
        services.AddSingleton<ISLMService>(sp => new OnnxSLMService(
            sp.GetRequiredService<TextGenerationEngine>()));

        services.AddSingleton<IVectorStorageService, FileVectorStorageService>();
        services.AddSingleton<ITextChunkingService, TextChunkingService>();
        services.AddSingleton<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddSingleton<IQueryProcessingService, QueryProcessingService>();
        services.AddSingleton<IContextRetrievalService, ContextRetrievalService>();
        services.AddSingleton<IResponseGenerationService, ResponseGenerationService>();
        services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

        // Agent / tools / security (Phase 4 Stage 2). The tool registry auto-discovers concrete
        // ITool implementations (e.g. ReadFileContents) via reflection. The orchestrator consumes the
        // RAG services (folded into its prompts) and runs the registered SLM. The security manager
        // uses the Avalonia HITL approval surface for modifying operations (Stage 3 T3.2).
        services.AddSingleton<IUserInteraction>(_ => Security.AvaloniaUserInteraction.FromApplication());
        services.AddSingleton<IToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            registry.DiscoverAndRegisterTools();
            return registry;
        });
        services.AddSingleton<ISecurityManager>(sp => new SecurityManager(sp.GetRequiredService<IUserInteraction>()));
        services.AddSingleton<IAgentOrchestrator>(sp =>
        {
            var orchestrator = new AgentOrchestrator(
                sp.GetRequiredService<IToolRegistry>(),
                sp.GetRequiredService<ISecurityManager>(),
                sp.GetRequiredService<IContextRetrievalService>(),
                sp.GetRequiredService<IQueryProcessingService>());
            orchestrator.RegisterSLMService(sp.GetRequiredService<ISLMService>());
            return orchestrator;
        });

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<OnboardingWizardViewModel>();
        services.AddTransient<BackgroundTaskViewModel>();
    }
}