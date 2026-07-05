using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SupportAssistant.Core.Agent;
using SupportAssistant.Core.Services;
using SupportAssistant.Core.Models;
using SupportAssistant.Models;
using ReactiveUI;
using System.Reactive;

namespace SupportAssistant.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IQueryProcessingService _queryProcessor;
    private readonly IContextRetrievalService _contextRetrieval;
    private readonly IResponseGenerationService _responseGenerationService;
    private readonly IAgentOrchestrator? _orchestrator;
    private CancellationTokenSource? _processingCts;

    private string _userInput = string.Empty;
    private bool _isProcessing = false;
    private string _statusMessage = "Ready";
    private bool _isTyping = false;
    private string _processingStage = string.Empty;
    private int _processingProgress = 0;

    // Events for UI updates
    public event Action? ScrollToBottom;
    public event Action<string>? CopyToClipboard;

    /// <summary>
    /// Creates the chat view model. The <paramref name="orchestrator"/> drives the tool-augmented
    /// agent loop when supplied (Phase 4 T2.4); when absent or when it errors, the plain RAG path
    /// (<see cref="IResponseGenerationService"/>) is used as the fallback.
    /// </summary>
    public ChatViewModel(
        IQueryProcessingService queryProcessor,
        IContextRetrievalService contextRetrieval,
        IResponseGenerationService responseGenerationService,
        IAgentOrchestrator? orchestrator = null)
    {
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        _contextRetrieval = contextRetrieval ?? throw new ArgumentNullException(nameof(contextRetrieval));
        _responseGenerationService = responseGenerationService ?? throw new ArgumentNullException(nameof(responseGenerationService));
        _orchestrator = orchestrator;

        Messages = new ThreadSafeObservableCollection<ChatMessage>();

        // Trigger auto-scroll whenever messages are added
        Messages.CollectionChanged += OnMessagesCollectionChanged;

        // Initialize with welcome message
        Messages.Add(new ChatMessage
        {
            Content = "Welcome to SupportAssistant! I'm here to help with your technical questions. What can I assist you with today?",
            Type = ChatMessageType.System
        });

        // Create observable for CanSendMessage based on UserInput and IsProcessing
        var canSendMessage = this.WhenAnyValue(x => x.UserInput, x => x.IsProcessing,
            (userInput, isProcessing) => !isProcessing && !string.IsNullOrWhiteSpace(userInput));
            
        // Create observable for RetryLastMessage (only needs to not be processing)
        var canRetryLastMessage = this.WhenAnyValue(x => x.IsProcessing, isProcessing => !isProcessing);
            
        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessageAsync, canSendMessage);
        ClearChatCommand = ReactiveCommand.Create(ClearChat);
        
        // Initialize new commands
        CopyMessageCommand = ReactiveCommand.Create<string>(CopyMessage);
        RetryLastMessageCommand = ReactiveCommand.CreateFromTask(RetryLastMessage, canRetryLastMessage);
        CancelProcessingCommand = ReactiveCommand.Create(CancelProcessing, this.WhenAnyValue(x => x.IsProcessing));
        
        // Keep CanSendMessage property in sync with the observable
        canSendMessage.Subscribe(value => CanSendMessage = value);
    }

    public ObservableCollection<ChatMessage> Messages { get; }

    public string UserInput
    {
        get => _userInput;
        set => this.RaiseAndSetIfChanged(ref _userInput, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    private bool _canSendMessage = false;
    
    public bool CanSendMessage
    {
        get => _canSendMessage;
        private set => this.RaiseAndSetIfChanged(ref _canSendMessage, value);
    }

    public ICommand SendMessageCommand { get; }
    public ICommand ClearChatCommand { get; }

    // New commands for Phase 3.4
    public ICommand CopyMessageCommand { get; }
    public ICommand RetryLastMessageCommand { get; }
    public ICommand CancelProcessingCommand { get; }

    // New properties for advanced UI
    public bool IsTyping
    {
        get => _isTyping;
        set => this.RaiseAndSetIfChanged(ref _isTyping, value);
    }

    public string ProcessingStage
    {
        get => _processingStage;
        set => this.RaiseAndSetIfChanged(ref _processingStage, value);
    }

    public int ProcessingProgress
    {
        get => _processingProgress;
        set => this.RaiseAndSetIfChanged(ref _processingProgress, value);
    }

    public bool ShowTypingIndicator => IsProcessing && IsTyping;

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput))
            return;

        var userMessage = UserInput.Trim();
        UserInput = string.Empty;
        IsProcessing = true;
        _processingCts?.Dispose();
        _processingCts = new CancellationTokenSource();
        var cancellationToken = _processingCts.Token;

        try
        {
            // Add user message to chat
            Messages.Add(new ChatMessage
            {
                Content = userMessage,
                Type = ChatMessageType.User
            });

            StatusMessage = "Processing your question...";
            ProcessingStage = "Analyzing query...";
            ProcessingProgress = 20;

            string? responseContent;
            string statusSuffix;

            // Primary path: the tool-augmented agent loop when an orchestrator is wired (Phase 4 T2.4).
            // Falls back to the plain RAG path when the orchestrator is absent or errors.
            if (_orchestrator is not null)
            {
                ProcessingStage = "Running agent...";
                ProcessingProgress = 40;
                IsTyping = true;

                var userId = Environment.UserName;
                var agentResponse = await _orchestrator.ProcessQueryAsync(userId, userMessage, cancellationToken);
                responseContent = FormatAgentResponse(agentResponse);
                statusSuffix = $"({agentResponse.ProcessingTime.TotalMilliseconds:F0}ms)";
            }
            else
            {
                responseContent = await GenerateRagResponseAsync(userMessage, cancellationToken);
                statusSuffix = string.Empty;
            }

            ProcessingProgress = 100;
            IsTyping = false;

            // When neither path produced a response, surface it as a status without adding an empty
            // assistant message (mirrors the legacy RAG null-response behavior).
            if (string.IsNullOrEmpty(responseContent))
            {
                StatusMessage = "Unable to generate a response. Please try again.";
                ScrollToBottom?.Invoke();
                return;
            }

            // Add assistant response to chat
            Messages.Add(new ChatMessage
            {
                Content = responseContent,
                Type = ChatMessageType.Assistant
            });

            StatusMessage = string.IsNullOrEmpty(statusSuffix) ? "Response generated" : $"Response generated {statusSuffix}";
            ProcessingStage = "Complete";

            // Trigger auto-scroll to show new message
            ScrollToBottom?.Invoke();
        }
        catch (OperationCanceledException)
        {
            IsTyping = false;
            ProcessingProgress = 0;
            ProcessingStage = string.Empty;
            StatusMessage = "Processing cancelled";
        }
        catch (Exception ex)
        {
            IsTyping = false;
            ProcessingProgress = 0;
            ProcessingStage = "Error";

            Messages.Add(new ChatMessage
            {
                Content = $"I apologize, but I encountered an error while processing your request: {ex.Message}",
                Type = ChatMessageType.System
            });

            StatusMessage = "Error occurred during processing";
            ScrollToBottom?.Invoke();
        }
        finally
        {
            IsProcessing = false;
            IsTyping = false;
            ProcessingProgress = 0;
            ProcessingStage = string.Empty;
        }
    }

    /// <summary>
    /// Plain retrieval-augmented fallback (Phase 4 T2.4): process -> retrieve -> generate. Returns the
    /// formatted response text, or <see langword="null"/> when no response could be produced (so the
    /// caller can surface a status instead of an empty message). Honors <paramref name="cancellationToken"/>.
    /// </summary>
    private async Task<string?> GenerateRagResponseAsync(string userMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StatusMessage = "Retrieving relevant information...";
        ProcessingStage = "Searching knowledge base...";
        ProcessingProgress = 40;

        var processedQuery = await _queryProcessor.ProcessQueryAsync(userMessage);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await _contextRetrieval.RetrieveContextAsync(processedQuery);
        cancellationToken.ThrowIfCancellationRequested();

        StatusMessage = "Generating AI response...";
        ProcessingStage = "Generating response...";
        ProcessingProgress = 70;
        IsTyping = true;

        var response = await _responseGenerationService.GenerateResponseAsync(processedQuery, contextResult);
        return response == null ? null : FormatResponseWithMetadata(response);
    }

    /// <summary>
    /// Renders an <see cref="AgentResponse"/> for the chat, surfacing the tool executions and any
    /// errors/warnings as metadata beneath the response text.
    /// </summary>
    private static string FormatAgentResponse(AgentResponse response)
    {
        var content = string.IsNullOrWhiteSpace(response.ResponseText)
            ? "I was unable to produce a response."
            : response.ResponseText;

        if (response.ToolExecutions.Count > 0)
        {
            content += "\n\n🛠 **Tools used:** " +
                       string.Join(", ", response.ToolExecutions.Select(r => r.ToolCall.ToolName));

            var failed = response.ToolExecutions.Where(r => !r.ToolResult.IsSuccess).ToList();
            if (failed.Count > 0)
            {
                content += "\n❌ Failed: " + string.Join(", ", failed.Select(r => r.ToolCall.ToolName));
            }
        }

        if (response.Errors.Count > 0)
        {
            content += "\n\n⚠️ " + string.Join("; ", response.Errors);
        }

        return content;
    }

    private string FormatResponseWithMetadata(GeneratedResponse response)
    {
        var content = response.ResponseText;
        
        // Add source references if available
        if (response.SourceReferences.Length > 0)
        {
            content += "\n\n📚 **Sources:**\n";
            foreach (var source in response.SourceReferences.Take(3))
            {
                content += $"• {source.DocumentPath} (Relevance: {source.RelevanceScore:P1})\n";
            }
        }
        
        // Add confidence indicator for low confidence responses
        if (response.Confidence < 0.6f)
        {
            content += "\n\n⚠️ *This response has lower confidence. Please verify the information or ask for clarification.*";
        }
        
        return content;
    }

    private void ClearChat()
    {
        IsProcessing = false;
        IsTyping = false;
        Messages.Clear();
        Messages.Add(new ChatMessage
        {
            Content = "Chat cleared. How can I help you?",
            Type = ChatMessageType.System
        });
        StatusMessage = "Ready";
        ProcessingStage = string.Empty;
        ProcessingProgress = 0;

        // Trigger auto-scroll to show welcome message
        ScrollToBottom?.Invoke();
    }

    private void CopyMessage(string content)
    {
        if (!string.IsNullOrEmpty(content))
        {
            CopyToClipboard?.Invoke(content);
            StatusMessage = "Message copied to clipboard";
        }
    }

    private async Task RetryLastMessage()
    {
        var lastUserMessage = Messages.LastOrDefault(m => m.Type == ChatMessageType.User);
        if (lastUserMessage != null)
        {
            UserInput = lastUserMessage.Content;
            if (!string.IsNullOrWhiteSpace(UserInput))
            {
                await SendMessageAsync();
            }
        }
    }

    private void CancelProcessing()
    {
        if (IsProcessing)
        {
            // Cancel the in-flight agent/RAG pipeline (Phase 4 T2.4).
            _processingCts?.Cancel();

            IsProcessing = false;
            IsTyping = false;
            ProcessingProgress = 0;
            ProcessingStage = string.Empty;
            StatusMessage = "Processing cancelled";
        }
    }

    /// <summary>
    /// Raised when a message is added so the UI can auto-scroll to the bottom.
    /// </summary>
    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            ScrollToBottom?.Invoke();
        }
    }

    // Keyboard shortcut handlers
    public void HandleKeyboardShortcut(string shortcut)
    {
        switch (shortcut.ToLower())
        {
            case "enter":
            case "ctrl+enter":
                if (SendMessageCommand.CanExecute(null))
                    SendMessageCommand.Execute(null);
                break;
            case "shift+enter":
                // Insert a newline without sending
                UserInput += "\n";
                break;
            case "ctrl+l":
                if (ClearChatCommand.CanExecute(null))
                    ClearChatCommand.Execute(null);
                break;
            case "escape":
                if (CancelProcessingCommand.CanExecute(null))
                    CancelProcessingCommand.Execute(null);
                break;
            case "ctrl+c":
                var lastAssistantMessage = Messages.LastOrDefault(m => m.Type == ChatMessageType.Assistant);
                if (lastAssistantMessage != null)
                    CopyMessage(lastAssistantMessage.Content);
                break;
            case "ctrl+r":
                var lastUserMessage = Messages.LastOrDefault(m => m.Type == ChatMessageType.User);
                if (lastUserMessage != null)
                {
                    UserInput = lastUserMessage.Content;
                    StatusMessage = "Ready to retry message";
                }
                break;
        }
    }
}
