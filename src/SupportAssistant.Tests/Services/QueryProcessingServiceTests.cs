using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SupportAssistant.Core.Services;
using Xunit;

namespace SupportAssistant.Tests.Services;

public class QueryProcessingServiceTests
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly QueryProcessingService _queryProcessingService;

    public QueryProcessingServiceTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _queryProcessingService = new QueryProcessingService(_mockEmbeddingService.Object);
        
        // Default mock setup - return a simple embedding
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
    }

    [Fact]
    public void Constructor_WithNullEmbeddingService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new QueryProcessingService(null!));
    }

    [Fact]
    public async Task ProcessQueryAsync_WithValidQuery_ReturnsValidProcessedQuery()
    {
        // Arrange
        var query = "How do I reset my password?";
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await _queryProcessingService.ProcessQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.OriginalQuery.Should().Be(query);
        result.ProcessedText.Should().NotBeEmpty();
        result.Embedding.Should().BeEquivalentTo(expectedEmbedding);
        result.ValidationError.Should().BeNull();
        result.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessQueryAsync_WithEmptyQuery_ReturnsInvalidProcessedQuery()
    {
        // Act
        var result = await _queryProcessingService.ProcessQueryAsync("");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.OriginalQuery.Should().Be("");
        result.ProcessedText.Should().BeEmpty();
        result.Embedding.Should().BeEmpty();
        result.ValidationError.Should().NotBeNullOrEmpty();
        result.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessQueryAsync_WhenEmbeddingServiceThrows_ReturnsInvalidResult()
    {
        // Arrange
        var query = "Test query";
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service error"));

        // Act
        var result = await _queryProcessingService.ProcessQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.OriginalQuery.Should().Be(query);
        result.ProcessedText.Should().BeEmpty();
        result.Embedding.Should().BeEmpty();
        result.ValidationError.Should().Contain("Query processing failed");
        result.ValidationError.Should().Contain("Embedding service error");
    }

    [Fact]
    public void ValidateQuery_WithValidQuery_ReturnsTrue()
    {
        // Act
        var result = _queryProcessingService.ValidateQuery("How do I reset my password?");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateQuery_WithEmptyQuery_ReturnsFalse()
    {
        // Act
        var result = _queryProcessingService.ValidateQuery("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetValidationResult_WithValidQuery_ReturnsValidResult()
    {
        // Act
        var result = _queryProcessingService.GetValidationResult("How do I reset my password?");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void GetValidationResult_WithEmptyQuery_ReturnsInvalidResult()
    {
        // Act
        var result = _queryProcessingService.GetValidationResult("");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Query cannot be null or empty");
        result.Issues.Should().HaveCount(1);
    }

    [Fact]
    public void PreprocessQuery_WithValidInput_ReturnsNormalizedOutput()
    {
        // Act
        var result = _queryProcessingService.PreprocessQuery("  HELLO WORLD  ");

        // Assert
        result.Should().Be("hello world");
    }

    [Fact]
    public void PreprocessQuery_WithEmptyInput_ReturnsEmpty()
    {
        // Act
        var result = _queryProcessingService.PreprocessQuery("");

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public async Task ProcessQueryAsync_ValidQuery_HasCorrectMetadata()
    {
        // Arrange
        var query = "How do I reset my password?";
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await _queryProcessingService.ProcessQueryAsync(query);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.OriginalLength.Should().Be(query.Length);
        result.Metadata.ProcessedLength.Should().BeGreaterThan(0);
        result.Metadata.WordCount.Should().BeGreaterThan(0);
        result.Metadata.ProcessedWordCount.Should().BeGreaterThan(0);
        result.Metadata.EmbeddingDimension.Should().Be(expectedEmbedding.Length);
        result.Metadata.ProcessingTimeMs.Should().BeGreaterOrEqualTo(0);
    }
}
