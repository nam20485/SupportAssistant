using FluentAssertions;
using Moq;
using SupportAssistant.Core.Services;
using SupportAssistant.Models;
using SupportAssistant.ViewModels;
using Xunit;

namespace SupportAssistant.Tests.UI;

/// <summary>
/// Tests for the empty-chat example prompts feature: the two default prompts are shown
/// while only System messages exist, hidden once a user/assistant message appears, and
/// restored after clearing. Clicking a prompt fills the send box and requests focus
/// without sending.
/// </summary>
public class ExamplePromptsTests
{
    private readonly Mock<IQueryProcessingService> _mockQueryProcessor;
    private readonly Mock<IContextRetrievalService> _mockContextRetrieval;
    private readonly Mock<IResponseGenerationService> _mockResponseGeneration;
    private readonly ChatViewModel _viewModel;

    public ExamplePromptsTests()
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
    public void ExamplePrompts_ShouldContainTheTwoDefaultPrompts()
    {
        _viewModel.ExamplePrompts.Should().HaveCount(2);
        _viewModel.ExamplePrompts.Should().Contain("How do I change my desktop background?");
        _viewModel.ExamplePrompts.Should().Contain("How do I take a screenshot?");
    }

    [Fact]
    public void ShowExamplePrompts_ShouldBeTrueOnFreshChatWithOnlyWelcomeMessage()
    {
        _viewModel.Messages.Should().OnlyContain(m => m.Type == ChatMessageType.System);
        _viewModel.ShowExamplePrompts.Should().BeTrue();
    }

    [Fact]
    public void ShowExamplePrompts_ShouldBeFalseAfterUserMessageAdded()
    {
        _viewModel.Messages.Add(new ChatMessage { Content = "Hi", Type = ChatMessageType.User });

        _viewModel.ShowExamplePrompts.Should().BeFalse();
    }

    [Fact]
    public void ShowExamplePrompts_ShouldBeFalseAfterAssistantMessageAdded()
    {
        _viewModel.Messages.Add(new ChatMessage { Content = "Hello!", Type = ChatMessageType.Assistant });

        _viewModel.ShowExamplePrompts.Should().BeFalse();
    }

    [Fact]
    public void ShowExamplePrompts_ShouldBeRestoredAfterClearingChat()
    {
        _viewModel.Messages.Add(new ChatMessage { Content = "Hi", Type = ChatMessageType.User });
        _viewModel.ShowExamplePrompts.Should().BeFalse();

        _viewModel.ClearChatCommand.Execute(null);

        _viewModel.Messages.Should().OnlyContain(m => m.Type == ChatMessageType.System);
        _viewModel.ShowExamplePrompts.Should().BeTrue();
    }

    [Fact]
    public void UseExamplePromptCommand_ShouldFillTheSendBox()
    {
        _viewModel.UserInput = string.Empty;

        _viewModel.UseExamplePromptCommand.Execute("How do I take a screenshot?");

        _viewModel.UserInput.Should().Be("How do I take a screenshot?");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UseExamplePromptCommand_ShouldIgnoreBlankInput(string? prompt)
    {
        _viewModel.UserInput = "unchanged";

        _viewModel.UseExamplePromptCommand.Execute(prompt);

        _viewModel.UserInput.Should().Be("unchanged");
    }

    [Fact]
    public void UseExamplePromptCommand_ShouldNotAddAnyMessages()
    {
        var countBefore = _viewModel.Messages.Count;

        _viewModel.UseExamplePromptCommand.Execute("How do I change my desktop background?");

        _viewModel.Messages.Should().HaveCount(countBefore);
    }

    [Fact]
    public void UseExamplePromptCommand_ShouldRequestInputFocus()
    {
        bool focusRequested = false;
        _viewModel.FocusInputRequested += () => focusRequested = true;

        _viewModel.UseExamplePromptCommand.Execute("How do I take a screenshot?");

        focusRequested.Should().BeTrue();
    }

    [Fact]
    public void UseExamplePromptCommand_ShouldNotRequestFocusForBlankInput()
    {
        bool focusRequested = false;
        _viewModel.FocusInputRequested += () => focusRequested = true;

        _viewModel.UseExamplePromptCommand.Execute("");

        focusRequested.Should().BeFalse();
    }
}
