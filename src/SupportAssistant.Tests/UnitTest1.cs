using System.Linq;
using FluentAssertions;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Tests;

public class OnnxRuntimeServiceTests
{
    [Fact]
    public void Initialize_ShouldComplete()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act & Assert - Should not throw an exception
        var initialize = () => service.Initialize();
        
        initialize.Should().NotThrow();
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnProviders()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act
        var providers = service.GetAvailableProviders();

        // Assert
        providers.Should().NotBeNull();
        providers.Should().NotBeEmpty();
        providers.Should().Contain("CPUExecutionProvider");
    }

    [Fact]
    public void CreateSessionOptions_ShouldReturnValidSessionOptions()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act
        var sessionOptions = service.CreateSessionOptions();

        // Assert
        sessionOptions.Should().NotBeNull();
    }

    [Fact]
    public void IsDirectMLAvailable_ShouldComplete()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act & Assert - Should not throw an exception
        var checkDirectMl = () => service.IsDirectMLAvailable();
        
        checkDirectMl.Should().NotThrow();
    }
}

public class TextChunkingServiceTests
{
    [Fact]
    public void ChunkText_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var service = new TextChunkingService();

        // Act
        var chunks = service.ChunkText(string.Empty);

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunk()
    {
        // Arrange
        var service = new TextChunkingService();
        var text = "This is a short text.";

        // Act
        var chunks = service.ChunkText(text, maxChunkSize: 100).ToList();

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Content.Should().Be(text);
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[0].StartIndex.Should().Be(0);
    }

    [Fact]
    public void ChunkText_LongText_ReturnsMultipleChunks()
    {
        // Arrange
        var service = new TextChunkingService();
        var text = new string('A', 150); // 150 characters

        // Act
        var chunks = service.ChunkText(text, maxChunkSize: 100, overlapSize: 20).ToList();

        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        chunks[0].Content.Length.Should().Be(100);
        chunks[1].Content.Length.Should().Be(70); // 150 - 100 + 20 overlap
    }

    [Fact]
    public void ChunkTextIntelligent_TextWithSentences_BreaksAtSentenceBoundary()
    {
        // Arrange
        var service = new TextChunkingService();
        var text = "First sentence. " + new string('A', 90) + " Second sentence. " + new string('B', 90);

        // Act
        var chunks = service.ChunkTextIntelligent(text, maxChunkSize: 100, overlapSize: 10).ToList();

        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        chunks[0].Content.Should().EndWith("First sentence.");
    }

    [Fact]
    public void ChunkTextIntelligent_TextWithParagraphs_BreaksAtParagraphBoundary()
    {
        // Arrange
        var service = new TextChunkingService();
        var text = "First paragraph.\n\nSecond paragraph with more content.";

        // Act
        var chunks = service.ChunkTextIntelligent(text, maxChunkSize: 30, overlapSize: 5).ToList();

        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        // Should break at paragraph boundary when possible
        chunks.Should().Contain(c => c.Content.Contains("First paragraph."));
    }
}
