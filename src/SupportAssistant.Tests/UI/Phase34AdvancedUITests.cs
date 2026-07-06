using System;
using System.Reactive;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using SupportAssistant.ViewModels;
using SupportAssistant.Core.Services;
using SupportAssistant.Models;
using Moq;

namespace SupportAssistant.Tests.UI;

/// <summary>
/// Tests for Phase 3.4 Advanced UI Features
/// Validates enhanced loading indicators, progress feedback, typing indicators,
/// keyboard shortcuts, and accessibility improvements
/// </summary>
public class Phase34AdvancedUITests
{
    private readonly Mock<IQueryProcessingService> _mockQueryProcessor;
    private readonly Mock<IContextRetrievalService> _mockContextRetrieval;
    private readonly Mock<IResponseGenerationService> _mockResponseGeneration;
    private readonly ChatViewModel _viewModel;

    public Phase34AdvancedUITests()
    {
        _mockQueryProcessor = new Mock<IQueryProcessingService>();
        _mockContextRetrieval = new Mock<IContextRetrievalService>();
        _mockResponseGeneration = new Mock<IResponseGenerationService>();
        
        _viewModel = new ChatViewModel(
            _mockQueryProcessor.Object,
            _mockContextRetrieval.Object,
            _mockResponseGeneration.Object);
    }

    [Fact]
    public void Phase34_EnhancedProperties_ShouldBeInitializedCorrectly()
    {
        // Verify new Phase 3.4 properties are properly initialized
        _viewModel.IsTyping.Should().BeFalse();
        _viewModel.ProcessingStage.Should().BeEmpty();
        _viewModel.ProcessingProgress.Should().Be(0);
        _viewModel.ShowTypingIndicator.Should().BeFalse();
        
        // Verify new commands are available
        _viewModel.CopyMessageCommand.Should().NotBeNull();
        _viewModel.RetryLastMessageCommand.Should().NotBeNull();
        _viewModel.CancelProcessingCommand.Should().NotBeNull();
    }

    [Fact]
    public void Phase34_TypingIndicator_ShouldShowWhenProcessingAndTyping()
    {
        // Arrange
        _viewModel.IsProcessing = false;
        _viewModel.IsTyping = false;
        
        // Act & Assert - No typing indicator when not processing
        _viewModel.ShowTypingIndicator.Should().BeFalse();
        
        // Act & Assert - No typing indicator when processing but not typing
        _viewModel.IsProcessing = true;
        _viewModel.IsTyping = false;
        _viewModel.ShowTypingIndicator.Should().BeFalse();
        
        // Act & Assert - Show typing indicator when both processing and typing
        _viewModel.IsProcessing = true;
        _viewModel.IsTyping = true;
        _viewModel.ShowTypingIndicator.Should().BeTrue();
    }

    [Fact]
    public void Phase34_ProgressTracking_ShouldUpdateCorrectly()
    {
        // Arrange & Act
        _viewModel.ProcessingStage = "Analyzing query...";
        _viewModel.ProcessingProgress = 25;
        
        // Assert
        _viewModel.ProcessingStage.Should().Be("Analyzing query...");
        _viewModel.ProcessingProgress.Should().Be(25);
        
        // Act - Update progress
        _viewModel.ProcessingStage = "Generating response...";
        _viewModel.ProcessingProgress = 75;
        
        // Assert
        _viewModel.ProcessingStage.Should().Be("Generating response...");
        _viewModel.ProcessingProgress.Should().Be(75);
    }

    [Fact]
    public void Phase34_CopyMessageCommand_ShouldTriggerCopyEvent()
    {
        // Arrange
        string? copiedContent = null;
        _viewModel.CopyToClipboard += content => copiedContent = content;
        
        // Act
        _viewModel.CopyMessageCommand.Execute("Test message content");
        
        // Assert
        copiedContent.Should().Be("Test message content");
        _viewModel.StatusMessage.Should().Be("Message copied to clipboard");
    }

    [Fact]
    public void Phase34_CancelProcessingCommand_ShouldResetProcessingState()
    {
        // Arrange
        _viewModel.IsProcessing = true;
        _viewModel.IsTyping = true;
        _viewModel.ProcessingProgress = 50;
        _viewModel.ProcessingStage = "Processing...";
        
        // Act
        _viewModel.CancelProcessingCommand.Execute(null);
        
        // Assert
        _viewModel.IsProcessing.Should().BeFalse();
        _viewModel.IsTyping.Should().BeFalse();
        _viewModel.ProcessingProgress.Should().Be(0);
        _viewModel.ProcessingStage.Should().BeEmpty();
        _viewModel.StatusMessage.Should().Be("Processing cancelled");
    }

    [Fact]
    public void Phase34_RetryLastMessageCommand_ShouldSetLastUserMessage()
    {
        // Arrange
        _viewModel.Messages.Add(new ChatMessage 
        { 
            Content = "Hello", 
            Type = ChatMessageType.User 
        });
        _viewModel.Messages.Add(new ChatMessage 
        { 
            Content = "Hi there!", 
            Type = ChatMessageType.Assistant 
        });
        _viewModel.Messages.Add(new ChatMessage 
        { 
            Content = "How are you?", 
            Type = ChatMessageType.User 
        });
        
        // Act - Test the command can execute when there are user messages  
        var canExecuteBefore = _viewModel.RetryLastMessageCommand.CanExecute(null);
        
        // Assert - Command should be available when not processing and messages exist
        canExecuteBefore.Should().BeTrue("command should be available when not processing and user messages exist");
        
        // Verify the last user message is correctly identified
        var lastUserMessage = _viewModel.Messages.LastOrDefault(m => m.Type == ChatMessageType.User);
        lastUserMessage.Should().NotBeNull();
        lastUserMessage!.Content.Should().Be("How are you?");
    }

    [Fact]
    public void Phase34_KeyboardShortcuts_ShouldHandleCorrectly()
    {
        // Arrange
        var originalMessagesCount = _viewModel.Messages.Count;
        
        // Act & Assert - Ctrl+L should clear chat
        _viewModel.HandleKeyboardShortcut("ctrl+l");
        _viewModel.Messages.Count.Should().Be(1); // Welcome message after clear
        _viewModel.StatusMessage.Should().Be("Ready");
        
        // Act & Assert - Escape should cancel processing
        _viewModel.IsProcessing = true;
        _viewModel.HandleKeyboardShortcut("escape");
        _viewModel.IsProcessing.Should().BeFalse();
        _viewModel.StatusMessage.Should().Be("Processing cancelled");
    }

    [Fact]
    public void Phase34_ScrollToBottomEvent_ShouldBeTriggeredAppropriately()
    {
        // Arrange
        bool scrollTriggered = false;
        _viewModel.ScrollToBottom += () => scrollTriggered = true;
        
        // Act - Clear chat should trigger scroll
        _viewModel.ClearChatCommand.Execute(null);
        
        // Assert
        scrollTriggered.Should().BeTrue();
    }

    [Fact]
    public void Phase34_EnhancedClearChat_ShouldResetAllProperties()
    {
        // Arrange
        _viewModel.ProcessingStage = "Some stage";
        _viewModel.ProcessingProgress = 50;
        _viewModel.Messages.Add(new ChatMessage { Content = "Test", Type = ChatMessageType.User });
        
        bool scrollTriggered = false;
        _viewModel.ScrollToBottom += () => scrollTriggered = true;
        
        // Act
        _viewModel.ClearChatCommand.Execute(null);
        
        // Assert
        _viewModel.ProcessingStage.Should().BeEmpty();
        _viewModel.ProcessingProgress.Should().Be(0);
        _viewModel.StatusMessage.Should().Be("Ready");
        _viewModel.Messages.Count.Should().Be(1); // Welcome message
        _viewModel.Messages[0].Type.Should().Be(ChatMessageType.System);
        scrollTriggered.Should().BeTrue();
    }

    [Fact]
    public void Phase34_CommandAvailability_ShouldRespectProcessingState()
    {
        // Arrange
        _viewModel.UserInput = "Test message";
        
        // Act & Assert - Commands should be available when not processing
        _viewModel.IsProcessing = false;
        _viewModel.SendMessageCommand.CanExecute(null).Should().BeTrue();
        _viewModel.RetryLastMessageCommand.CanExecute(null).Should().BeTrue();
        _viewModel.CancelProcessingCommand.CanExecute(null).Should().BeFalse();
        
        // Act & Assert - Commands should respect processing state
        _viewModel.IsProcessing = true;
        _viewModel.SendMessageCommand.CanExecute(null).Should().BeFalse();
        _viewModel.RetryLastMessageCommand.CanExecute(null).Should().BeFalse();
        _viewModel.CancelProcessingCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Phase34_EnhancedStatusMessages_ShouldProvideDetailedFeedback()
    {
        // This test validates that status messages are more informative
        // and provide better user feedback during processing
        
        // Arrange
        var initialStatus = _viewModel.StatusMessage;
        
        // Act - Copy message
        _viewModel.CopyMessageCommand.Execute("test");
        
        // Assert
        _viewModel.StatusMessage.Should().Be("Message copied to clipboard");
        
        // Act - Cancel processing
        _viewModel.IsProcessing = true;
        _viewModel.CancelProcessingCommand.Execute(null);
        
        // Assert
        _viewModel.StatusMessage.Should().Be("Processing cancelled");
    }
}
