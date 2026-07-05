using Microsoft.Extensions.DependencyInjection;
using SupportAssistant.Core.Agent;
using SupportAssistant.Core.Security;
using SupportAssistant.Core.Services;
using SupportAssistant.Core.Tools;
using SupportAssistant.ViewModels;

namespace SupportAssistant.Tests.UI;

/// <summary>
/// Verifies the DI resolution that App.axaml.cs performs when the onboarding
/// wizard closes after Finish is clicked:
///     _serviceProvider.GetRequiredService&lt;MainWindowViewModel&gt;()
/// </summary>
public class MainWindowViewModelResolutionTests
{
    /// <summary>Mirrors App.ConfigureServices exactly.</summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISettingsService, SettingsService>();
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

        // Agent / tools / security (Phase 4 Stage 2).
        services.AddSingleton<IToolRegistry>(_ =>
        {
            var registry = new ToolRegistry();
            registry.DiscoverAndRegisterTools();
            return registry;
        });
        services.AddSingleton<ISecurityManager, SecurityManager>();
        services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<OnboardingWizardViewModel>();
        services.AddTransient<BackgroundTaskViewModel>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Resolving_MainWindowViewModel_Succeeds_AfterDiFix()
    {
        using var provider = BuildProvider();

        var vm = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(vm);
        Assert.NotNull(vm.Chat);
        Assert.NotNull(vm.BackgroundTask);
        Assert.NotNull(vm.OpenSettingsCommand);
        Assert.NotNull(vm.ExitCommand);
        Assert.NotNull(vm.AboutCommand);
    }
}
