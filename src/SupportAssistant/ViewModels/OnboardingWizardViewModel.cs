using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using SupportAssistant.Core.Models;
using SupportAssistant.Core.Services;

namespace SupportAssistant.ViewModels;

/// <summary>
/// ViewModel for the first-run onboarding wizard
/// </summary>
public class OnboardingWizardViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private int _currentStep = 0;
    private bool _isCompleted = false;
    private string _statusMessage = string.Empty;
    
    // Step 1: Welcome
    private bool _welcomeAcknowledged = false;
    
    // Step 2: Model Configuration
    private string _modelPath = string.Empty;
    private string _selectedExecutionProvider = "CPU";
    private bool _modelPathValid = false;
    
    // Step 3: Knowledge Base Setup
    private string _knowledgeBasePath = string.Empty;
    private bool _knowledgeBasePathValid = false;
    private bool _createSampleKnowledgeBase = true;
    
    public OnboardingWizardViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        // Initialize with default paths
        _modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "SupportAssistant", "Models");
        _knowledgeBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "SupportAssistant", "KnowledgeBase");
        
        // Commands
        NextCommand = ReactiveCommand.CreateFromTask(NextAsync);
        BackCommand = ReactiveCommand.Create(Back, this.WhenAnyValue(x => x.CanGoBack));
        FinishCommand = ReactiveCommand.CreateFromTask(FinishAsync, this.WhenAnyValue(x => x.CanFinish));
        CancelCommand = ReactiveCommand.Create(Cancel);
        BrowseModelPathCommand = ReactiveCommand.Create(BrowseModelPath);
        BrowseKnowledgeBasePathCommand = ReactiveCommand.Create(BrowseKnowledgeBasePath);
        ValidateModelPathCommand = ReactiveCommand.CreateFromTask(ValidateModelPathAsync);
        CreateDirectoriesCommand = ReactiveCommand.CreateFromTask(CreateDirectoriesAsync);
    }

    #region Properties

    public int CurrentStep
    {
        get => _currentStep;
        set => this.RaiseAndSetIfChanged(ref _currentStep, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => this.RaiseAndSetIfChanged(ref _isCompleted, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    // Step 1: Welcome
    public bool WelcomeAcknowledged
    {
        get => _welcomeAcknowledged;
        set => this.RaiseAndSetIfChanged(ref _welcomeAcknowledged, value);
    }

    // Step 2: Model Configuration
    public string ModelPath
    {
        get => _modelPath;
        set => this.RaiseAndSetIfChanged(ref _modelPath, value);
    }

    public string SelectedExecutionProvider
    {
        get => _selectedExecutionProvider;
        set => this.RaiseAndSetIfChanged(ref _selectedExecutionProvider, value);
    }

    public bool ModelPathValid
    {
        get => _modelPathValid;
        set => this.RaiseAndSetIfChanged(ref _modelPathValid, value);
    }

    // Step 3: Knowledge Base Setup
    public string KnowledgeBasePath
    {
        get => _knowledgeBasePath;
        set => this.RaiseAndSetIfChanged(ref _knowledgeBasePath, value);
    }

    public bool KnowledgeBasePathValid
    {
        get => _knowledgeBasePathValid;
        set => this.RaiseAndSetIfChanged(ref _knowledgeBasePathValid, value);
    }

    public bool CreateSampleKnowledgeBase
    {
        get => _createSampleKnowledgeBase;
        set => this.RaiseAndSetIfChanged(ref _createSampleKnowledgeBase, value);
    }

    #endregion

    #region Navigation Properties

    public bool CanGoBack => CurrentStep > 0;
    public bool CanGoNext => GetCanGoNext();
    public bool CanFinish => CurrentStep == 3 && GetCanFinish();

    public string CurrentStepTitle => CurrentStep switch
    {
        0 => "Welcome to SupportAssistant",
        1 => "Model Configuration",
        2 => "Knowledge Base Setup",
        3 => "Setup Complete",
        _ => "Unknown Step"
    };

    public string CurrentStepDescription => CurrentStep switch
    {
        0 => "Welcome! Let's get you set up with SupportAssistant.",
        1 => "Configure your AI model settings for optimal performance.",
        2 => "Set up your knowledge base for contextual assistance.",
        3 => "Your setup is complete! You're ready to start using SupportAssistant.",
        _ => "Unknown step description"
    };

    #endregion

    #region Available Options

    public List<string> AvailableExecutionProviders { get; } = new() { "CPU", "CUDA", "DirectML" };

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }
    public ReactiveCommand<Unit, Unit> FinishCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseModelPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseKnowledgeBasePathCommand { get; }
    public ReactiveCommand<Unit, Unit> ValidateModelPathCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateDirectoriesCommand { get; }

    #endregion

    #region Command Implementations

    private async Task NextAsync()
    {
        if (!CanGoNext) return;

        switch (CurrentStep)
        {
            case 0: // Welcome -> Model Configuration
                CurrentStep = 1;
                await ValidateModelPathAsync();
                break;
            case 1: // Model Configuration -> Knowledge Base Setup
                CurrentStep = 2;
                await ValidateKnowledgeBasePathAsync();
                break;
            case 2: // Knowledge Base Setup -> Complete
                CurrentStep = 3;
                break;
        }

        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(CanFinish));
        StatusMessage = string.Empty;
    }

    private void Back()
    {
        if (!CanGoBack) return;

        CurrentStep--;
        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(CanFinish));
        StatusMessage = string.Empty;
    }

    private async Task FinishAsync()
    {
        try
        {
            StatusMessage = "Applying settings...";

            // Create directories if they don't exist
            await CreateDirectoriesAsync();

            // Apply settings and mark setup as complete
            var settings = _settingsService.Settings;
            settings.Ai.ModelPath = ModelPath;
            settings.Ai.ExecutionProvider = SelectedExecutionProvider;
            settings.KnowledgeBase.KnowledgeBasePath = KnowledgeBasePath;
            settings.General.IsFirstRun = false; // Mark first run as complete

            await _settingsService.SaveSettingsAsync();

            // Create sample knowledge base if requested
            if (CreateSampleKnowledgeBase)
            {
                await CreateSampleKnowledgeBaseAsync();
            }

            IsCompleted = true;
            StatusMessage = "Setup completed successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error completing setup: {ex.Message}";
        }
    }

    private void Cancel()
    {
        IsCompleted = false;
        // Close wizard without saving
    }

    private void BrowseModelPath()
    {
        // TODO: Implement folder browser dialog
        StatusMessage = "Folder browser not yet implemented";
    }

    private void BrowseKnowledgeBasePath()
    {
        // TODO: Implement folder browser dialog
        StatusMessage = "Folder browser not yet implemented";
    }

    private Task ValidateModelPathAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ModelPath))
            {
                ModelPathValid = false;
                StatusMessage = "Model path is required";
                return Task.CompletedTask;
            }

            // Create directory if it doesn't exist
            if (!Directory.Exists(ModelPath))
            {
                Directory.CreateDirectory(ModelPath);
            }

            ModelPathValid = Directory.Exists(ModelPath);
            StatusMessage = ModelPathValid ? "Model path is valid" : "Model path is invalid";
        }
        catch (Exception ex)
        {
            ModelPathValid = false;
            StatusMessage = $"Error validating model path: {ex.Message}";
        }

        this.RaisePropertyChanged(nameof(CanGoNext));
        return Task.CompletedTask;
    }

    private Task ValidateKnowledgeBasePathAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(KnowledgeBasePath))
            {
                KnowledgeBasePathValid = false;
                StatusMessage = "Knowledge base path is required";
                return Task.CompletedTask;
            }

            // Create directory if it doesn't exist
            if (!Directory.Exists(KnowledgeBasePath))
            {
                Directory.CreateDirectory(KnowledgeBasePath);
            }

            KnowledgeBasePathValid = Directory.Exists(KnowledgeBasePath);
            StatusMessage = KnowledgeBasePathValid ? "Knowledge base path is valid" : "Knowledge base path is invalid";
        }
        catch (Exception ex)
        {
            KnowledgeBasePathValid = false;
            StatusMessage = $"Error validating knowledge base path: {ex.Message}";
        }

        this.RaisePropertyChanged(nameof(CanGoNext));
        return Task.CompletedTask;
    }

    private Task CreateDirectoriesAsync()
    {
        try
        {
            if (!Directory.Exists(ModelPath))
            {
                Directory.CreateDirectory(ModelPath);
            }

            if (!Directory.Exists(KnowledgeBasePath))
            {
                Directory.CreateDirectory(KnowledgeBasePath);
            }

            StatusMessage = "Directories created successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating directories: {ex.Message}";
        }
        
        return Task.CompletedTask;
    }

    private async Task CreateSampleKnowledgeBaseAsync()
    {
        try
        {
            var sampleFile = Path.Combine(KnowledgeBasePath, "getting-started.txt");
            if (!File.Exists(sampleFile))
            {
                var sampleContent = @"# Getting Started with SupportAssistant

Welcome to SupportAssistant! This is a sample knowledge base file to help you get started.

## How to Use SupportAssistant

1. Ask questions in the chat interface
2. SupportAssistant will search this knowledge base for relevant information
3. You'll receive AI-powered responses based on your documentation

## Adding Your Own Content

1. Add text files (.txt), Markdown files (.md), or PDF files to this knowledge base directory
2. SupportAssistant will automatically index new files
3. Your content will be available for contextual assistance

## Tips for Better Results

- Use clear, descriptive language in your questions
- Include relevant keywords from your documentation
- Break complex questions into smaller parts

This sample file can be safely deleted once you've added your own content.
";
                await File.WriteAllTextAsync(sampleFile, sampleContent);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating sample knowledge base: {ex.Message}";
        }
    }

    #endregion

    #region Helper Methods

    private bool GetCanGoNext()
    {
        return CurrentStep switch
        {
            0 => WelcomeAcknowledged,
            1 => ModelPathValid,
            2 => KnowledgeBasePathValid,
            _ => false
        };
    }

    private bool GetCanFinish()
    {
        return ModelPathValid && KnowledgeBasePathValid;
    }

    #endregion
}
