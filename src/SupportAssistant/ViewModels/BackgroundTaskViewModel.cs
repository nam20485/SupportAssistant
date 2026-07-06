using System;
using System.Reactive;
using System.Threading;
using ReactiveUI;
using SupportAssistant.Core.Services;

namespace SupportAssistant.ViewModels;

/// <summary>
/// ViewModel for displaying background task progress
/// </summary>
public class BackgroundTaskViewModel : ViewModelBase
{
    private readonly IBackgroundTaskService _backgroundTaskService;
    private bool _isRunning = false;
    private double _progress = 0.0;
    private string _statusMessage = "Ready";
    private bool _isVisible = false;

    public BackgroundTaskViewModel(IBackgroundTaskService backgroundTaskService)
    {
        _backgroundTaskService = backgroundTaskService;
        
        // Subscribe to service events
        _backgroundTaskService.TaskStarted += OnTaskStarted;
        _backgroundTaskService.ProgressChanged += OnProgressChanged;
        _backgroundTaskService.StatusChanged += OnStatusChanged;
        _backgroundTaskService.TaskCompleted += OnTaskCompleted;
        
        // Commands
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.IsRunning));
        DismissCommand = ReactiveCommand.Create(Dismiss, this.WhenAnyValue(x => x.IsRunning, x => !x));
        StartInitializationCommand = ReactiveCommand.CreateFromTask(StartInitialization, 
            this.WhenAnyValue(x => x.IsRunning, x => !x));
    }

    #region Properties

    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public double Progress
    {
        get => _progress;
        private set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public string ProgressText => $"{(Progress * 100):F0}%";

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> DismissCommand { get; }
    public ReactiveCommand<Unit, Unit> StartInitializationCommand { get; }

    #endregion

    #region Command Implementations

    private void Cancel()
    {
        _backgroundTaskService.CancelTasks();
    }

    private void Dismiss()
    {
        IsVisible = false;
    }

    private async System.Threading.Tasks.Task StartInitialization()
    {
        IsVisible = true;
        await _backgroundTaskService.StartInitializationAsync();
    }

    #endregion

    #region Event Handlers

    private void OnTaskStarted(object? sender, EventArgs e)
    {
        IsRunning = true;
        IsVisible = true;
        Progress = 0.0;
        StatusMessage = "Starting...";
    }

    private void OnProgressChanged(object? sender, double progress)
    {
        Progress = progress;
        this.RaisePropertyChanged(nameof(ProgressText));
    }

    private void OnStatusChanged(object? sender, string status)
    {
        StatusMessage = status;
    }

    private void OnTaskCompleted(object? sender, bool success)
    {
        IsRunning = false;
        
        if (success)
        {
            StatusMessage = "Initialization completed successfully!";
            // Auto-hide after a delay
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => 
            {
                if (!IsRunning)
                {
                    IsVisible = false;
                }
            });
        }
        else
        {
            StatusMessage = "Initialization failed. You can retry or continue without background initialization.";
        }
    }

    #endregion
}
