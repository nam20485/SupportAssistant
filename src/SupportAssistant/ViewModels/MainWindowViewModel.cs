using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using SupportAssistant.Core.Services;
using SupportAssistant.Views;

namespace SupportAssistant.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ChatViewModel _chatViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly BackgroundTaskViewModel _backgroundTaskViewModel;

    public MainWindowViewModel(
        IQueryProcessingService queryProcessor,
        IContextRetrievalService contextRetrieval,
        IResponseGenerationService responseGenerationService,
        SettingsViewModel settingsViewModel,
        BackgroundTaskViewModel backgroundTaskViewModel, ChatViewModel chatViewModel, ReactiveCommand<Unit, Unit> openSettingsCommand, ReactiveCommand<Unit, Unit> exitCommand, ReactiveCommand<Unit, Unit> aboutCommand)
    {
        _chatViewModel = new ChatViewModel(queryProcessor, contextRetrieval, responseGenerationService);
        _settingsViewModel = settingsViewModel;
        _backgroundTaskViewModel = backgroundTaskViewModel;
        
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettings);
        ExitCommand = ReactiveCommand.Create(Exit);
        AboutCommand = ReactiveCommand.CreateFromTask(ShowAbout);
    }

    public MainWindowViewModel(ChatViewModel chatViewModel, SettingsViewModel settingsViewModel, BackgroundTaskViewModel backgroundTaskViewModel, ReactiveCommand<Unit, Unit> openSettingsCommand, ReactiveCommand<Unit, Unit> exitCommand, ReactiveCommand<Unit, Unit> aboutCommand)
    {
        _chatViewModel = chatViewModel;
        _settingsViewModel = settingsViewModel;
        _backgroundTaskViewModel = backgroundTaskViewModel;
        OpenSettingsCommand = openSettingsCommand;
        ExitCommand = exitCommand;
        AboutCommand = aboutCommand;
        //throw new System.NotImplementedException();
    }

    public ChatViewModel Chat => _chatViewModel;
    public BackgroundTaskViewModel BackgroundTask => _backgroundTaskViewModel;
    public string Greeting { get; } = "Welcome to SupportAssistant!";
    
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }

    private async Task OpenSettings()
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = _settingsViewModel
        };
        
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await settingsWindow.ShowDialog(desktop.MainWindow);
        }
    }

    private void Exit()
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private async Task ShowAbout()
    {
        var aboutDialog = new AboutWindow();
        
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await aboutDialog.ShowDialog(desktop.MainWindow);
        }
    }
}
