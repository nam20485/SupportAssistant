using System;
using System.Threading;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for managing background compilation and setup tasks
/// </summary>
public interface IBackgroundTaskService
{
    /// <summary>
    /// Whether a background task is currently running
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Current task progress (0.0 to 1.0)
    /// </summary>
    double Progress { get; }
    
    /// <summary>
    /// Current task status message
    /// </summary>
    string StatusMessage { get; }
    
    /// <summary>
    /// Event fired when a background task starts
    /// </summary>
    event EventHandler? TaskStarted;
    
    /// <summary>
    /// Event fired when progress changes
    /// </summary>
    event EventHandler<double>? ProgressChanged;
    
    /// <summary>
    /// Event fired when status message changes
    /// </summary>
    event EventHandler<string>? StatusChanged;
    
    /// <summary>
    /// Event fired when a background task completes
    /// </summary>
    event EventHandler<bool>? TaskCompleted; // bool indicates success
    
    /// <summary>
    /// Start background initialization tasks after first setup
    /// </summary>
    Task StartInitializationAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancel any running background tasks
    /// </summary>
    void CancelTasks();
}

/// <summary>
/// Background task service implementation
/// </summary>
public class BackgroundTaskService : IBackgroundTaskService
{
    private readonly ISettingsService _settingsService;
    private readonly IOnnxRuntimeService _onnxService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private CancellationTokenSource? _cancellationTokenSource;
    
    private bool _isRunning = false;
    private double _progress = 0.0;
    private string _statusMessage = string.Empty;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            _isRunning = value;
            if (value)
                TaskStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    public double Progress
    {
        get => _progress;
        private set
        {
            _progress = value;
            ProgressChanged?.Invoke(this, value);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            StatusChanged?.Invoke(this, value);
        }
    }

    public event EventHandler? TaskStarted;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<bool>? TaskCompleted;

    public BackgroundTaskService(
        ISettingsService settingsService,
        IOnnxRuntimeService onnxService,
        IKnowledgeBaseService knowledgeBaseService)
    {
        _settingsService = settingsService;
        _onnxService = onnxService;
        _knowledgeBaseService = knowledgeBaseService;
    }

    public async Task StartInitializationAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;

        try
        {
            IsRunning = true;
            Progress = 0.0;
            StatusMessage = "Starting initialization...";

            // Phase 1: Validate model configuration (25%)
            await ValidateModelConfigurationAsync(token);
            Progress = 0.25;

            // Phase 2: Initialize ONNX Runtime (50%)
            await InitializeOnnxRuntimeAsync(token);
            Progress = 0.50;

            // Phase 3: Index knowledge base (75%)
            await IndexKnowledgeBaseAsync(token);
            Progress = 0.75;

            // Phase 4: Finalize initialization (100%)
            await FinalizeInitializationAsync(token);
            Progress = 1.0;
            StatusMessage = "Initialization completed successfully!";

            TaskCompleted?.Invoke(this, true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Initialization was cancelled.";
            TaskCompleted?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization failed: {ex.Message}";
            TaskCompleted?.Invoke(this, false);
        }
        finally
        {
            IsRunning = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public void CancelTasks()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task ValidateModelConfigurationAsync(CancellationToken cancellationToken)
    {
        StatusMessage = "Validating AI model configuration...";
        
        var settings = _settingsService.Settings;
        
        // Check if model path exists
        if (string.IsNullOrWhiteSpace(settings.Ai.ModelPath))
        {
            throw new InvalidOperationException("Model path is not configured.");
        }

        // Create model directory if it doesn't exist
        if (!System.IO.Directory.Exists(settings.Ai.ModelPath))
        {
            System.IO.Directory.CreateDirectory(settings.Ai.ModelPath);
        }

        // Simulate validation delay
        await Task.Delay(1000, cancellationToken);
    }

    private async Task InitializeOnnxRuntimeAsync(CancellationToken cancellationToken)
    {
        StatusMessage = "Initializing AI runtime...";
        
        try
        {
            // Initialize ONNX Runtime with configured provider
            var isDirectMLAvailable = _onnxService.Initialize();
            
            if (isDirectMLAvailable)
            {
                StatusMessage = "AI runtime initialized with DirectML acceleration.";
            }
            else
            {
                StatusMessage = "AI runtime initialized with CPU execution.";
            }
            
            // Simulate some async work
            await Task.Delay(1000, cancellationToken);
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI runtime initialization failed: {ex.Message}";
            throw;
        }
    }

    private async Task IndexKnowledgeBaseAsync(CancellationToken cancellationToken)
    {
        StatusMessage = "Indexing knowledge base...";
        
        try
        {
            var settings = _settingsService.Settings;
            
            if (!string.IsNullOrWhiteSpace(settings.KnowledgeBase.KnowledgeBasePath) &&
                System.IO.Directory.Exists(settings.KnowledgeBase.KnowledgeBasePath))
            {
                // Initialize knowledge base service
                var success = await _knowledgeBaseService.InitializeAsync();
                
                if (success)
                {
                    StatusMessage = "Knowledge base indexed successfully.";
                }
                else
                {
                    StatusMessage = "Knowledge base initialization failed.";
                }
            }
            else
            {
                StatusMessage = "Knowledge base path not configured, skipping indexing.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Knowledge base indexing failed: {ex.Message}";
            throw;
        }
    }

    private async Task FinalizeInitializationAsync(CancellationToken cancellationToken)
    {
        StatusMessage = "Finalizing initialization...";
        
        // Update settings to indicate initialization is complete
        var settings = _settingsService.Settings;
        settings.General.IsFirstRun = false;
        await _settingsService.SaveSettingsAsync();
        
        // Additional finalization tasks can be added here
        await Task.Delay(500, cancellationToken);
        
        StatusMessage = "Ready to assist!";
    }
}
