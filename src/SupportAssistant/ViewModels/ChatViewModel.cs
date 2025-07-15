using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
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
    
    private string _userInput = string.Empty;
    private bool _isProcessing = false;
    private string _statusMessage = "Ready";
    private bool _isTyping = false;
    private string _processingStage = string.Empty;
    private int _processingProgress = 0;

    // Events for UI updates
    public event Action? ScrollToBottom;
    public event Action<string>? CopyToClipboard;

    public ChatViewModel(
        IQueryProcessingService queryProcessor,
        IContextRetrievalService contextRetrieval,
        IResponseGenerationService responseGenerationService)
    {
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        _contextRetrieval = contextRetrieval ?? throw new ArgumentNullException(nameof(contextRetrieval));
        _responseGenerationService = responseGenerationService ?? throw new ArgumentNullException(nameof(responseGenerationService));

        Messages = new ObservableCollection<ChatMessage>();
        
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

            // Process the query through the RAG pipeline
            var processedQuery = await _queryProcessor.ProcessQueryAsync(userMessage);
            
            StatusMessage = "Retrieving relevant information...";
            ProcessingStage = "Searching knowledge base...";
            ProcessingProgress = 40;
            
            var contextResult = await _contextRetrieval.RetrieveContextAsync(processedQuery);
            
            StatusMessage = "Generating AI response...";
            ProcessingStage = "Generating response...";
            ProcessingProgress = 70;
            IsTyping = true;
            
            // Generate AI response using the full service
            var response = await _responseGenerationService.GenerateResponseAsync(processedQuery, contextResult);

            ProcessingProgress = 100;
            IsTyping = false;

            // Format response with metadata
            var responseContent = FormatResponseWithMetadata(response);

            // Add assistant response to chat
            Messages.Add(new ChatMessage
            {
                Content = responseContent,
                Type = ChatMessageType.Assistant
            });

            var confidenceText = response.Metadata.HasHighConfidence ? "High" : "Medium";
            StatusMessage = $"Response generated (Confidence: {confidenceText}, {response.Metadata.ProcessingTimeMs}ms)";
            ProcessingStage = "Complete";
            
            // Trigger auto-scroll to show new message
            ScrollToBottom?.Invoke();
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

    private async Task<string> GenerateSimpleResponse(string userQuery, ContextRetrievalResult contextResult)
    {
        await Task.Delay(500); // Simulate some processing time
        
        if (contextResult.Documents.Length == 0)
        {
            return "I couldn't find any relevant information in my knowledge base to answer your question. Please try rephrasing your question or asking about a different topic.";
        }

        var response = $"Based on the information in my knowledge base, here's what I found regarding your question about \"{userQuery}\":\n\n";
        
        foreach (var doc in contextResult.Documents.Take(3)) // Show top 3 results
        {
            response += $"📄 **{doc.Document.Source}**\n";
            response += $"{doc.Document.Content.Substring(0, Math.Min(doc.Document.Content.Length, 200))}...\n";
            response += $"(Relevance: {doc.SimilarityScore:P1})\n\n";
        }

        response += $"Retrieved from {contextResult.Documents.Length} knowledge base sources.";
        
        return response;
    }

    private void ClearChat()
    {
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
            // In a real implementation, this would cancel the ongoing operations
            IsProcessing = false;
            IsTyping = false;
            ProcessingProgress = 0;
            ProcessingStage = string.Empty;
            StatusMessage = "Processing cancelled";
        }
    }

    // Keyboard shortcut handlers
    public void HandleKeyboardShortcut(string shortcut)
    {
        switch (shortcut.ToLower())
        {
            case "ctrl+l":
                if (ClearChatCommand.CanExecute(null))
                    ClearChatCommand.Execute(null);
                break;
            case "escape":
                if (CancelProcessingCommand.CanExecute(null))
                    CancelProcessingCommand.Execute(null);
                break;
            case "ctrl+enter":
                if (SendMessageCommand.CanExecute(null))
                    SendMessageCommand.Execute(null);
                break;
        }
    }
}
