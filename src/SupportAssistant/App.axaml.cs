using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using SupportAssistant.Core.Services;
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
                        
                        // Start background initialization
                        var backgroundTaskVm = ((MainWindowViewModel)mainWindow.DataContext).BackgroundTask;
                        _ = backgroundTaskVm.StartInitializationCommand.Execute();
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
        services.AddSingleton<IEmbeddingService, OnnxEmbeddingService>();
        services.AddSingleton<IVectorStorageService, FileVectorStorageService>();
        services.AddSingleton<ITextChunkingService, TextChunkingService>();
        services.AddSingleton<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddSingleton<IQueryProcessingService, QueryProcessingService>();
        services.AddSingleton<IContextRetrievalService, ContextRetrievalService>();
        services.AddSingleton<IResponseGenerationService, ResponseGenerationService>();
        services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<OnboardingWizardViewModel>();
        services.AddTransient<BackgroundTaskViewModel>();
    }
}