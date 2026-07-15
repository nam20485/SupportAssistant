using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
    private bool _legacyRocmPromptShown;

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

            // Persist settings must be loaded before any consumer reads ExecutionProvider /
            // IsFirstRun / etc. SettingsService starts with in-memory defaults only.
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            // Resolve off the UI thread: blocking GetResult() on the Avalonia context deadlocks
            // after LoadSettingsAsync's first await.
            Task.Run(() => settingsService.LoadSettingsAsync()).GetAwaiter().GetResult();

            // Check if this is the first run
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
                        AttachLegacyRocmGpuPrompt(mainWindow, settingsService, desktop);
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
                var mainWindow = new MainWindow
                {
                    DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
                };
                desktop.MainWindow = mainWindow;
                AttachLegacyRocmGpuPrompt(mainWindow, settingsService, desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// After the first window opens, prompt when the host is ROCm-ready except for a missing
    /// launch-time HSA override on a legacy ISA. Exit shuts down; Continue keeps CPU fallback.
    /// </summary>
    private void AttachLegacyRocmGpuPrompt(
        Window window,
        ISettingsService settingsService,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        EventHandler? handler = null;
        handler = async (_, _) =>
        {
            window.Opened -= handler!;
            await ShowLegacyRocmGpuPromptIfNeededAsync(window, settingsService, desktop).ConfigureAwait(true);
        };
        window.Opened += handler;
    }

    private async Task ShowLegacyRocmGpuPromptIfNeededAsync(
        Window owner,
        ISettingsService settingsService,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_legacyRocmPromptShown)
        {
            return;
        }

        RocmPreflightResult preflight;
        try
        {
            preflight = RocmHostPreflight.Evaluate();
        }
        catch
        {
            return;
        }

        if (!LegacyRocmGpuPrompt.ShouldPrompt(preflight, settingsService))
        {
            return;
        }

        _legacyRocmPromptShown = true;

        var dialog = new LegacyRocmGpuDialog(preflight);
        var continueWithCpu = await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);

        if (dialog.DontAskAgain)
        {
            settingsService.Settings.Ai.BypassLegacyRocmGpuDialog = true;
            try
            {
                await settingsService.SaveSettingsAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to persist BypassLegacyRocmGpuDialog: {ex.Message}");
            }
        }

        if (!continueWithCpu)
        {
            desktop.Shutdown();
        }
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
        //
        // WS5: both engines load in-process (GUI process). InferenceOptionsFactory.Create's
        // allowGpuInProcess defaults to false, so on Linux these two calls always get CPU-forced —
        // interim safety against the Mesa↔MIGraphX/comgr LLVM CommandLine collision (reproduced
        // 2026-07-10). This is the construction site to redirect through
        // OutOfProcessBaseInferenceEngine when WS5's real out-of-process worker lands (at which point
        // the CPU-forcing here should be removed). See docs/plans/inference-engine-integration-status.md
        // §C and inference-integration-implementation-plan.md §6.
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
