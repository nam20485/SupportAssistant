using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SupportAssistant.Core.Models;
using SupportAssistant.Core.Services;
using Xunit;

namespace SupportAssistant.Tests.Services;

public class ContextRetrievalServiceTests
{
    private readonly Mock<IKnowledgeBaseService> _mockKnowledgeBaseService;
    private readonly Mock<IQueryProcessingService> _mockQueryProcessingService;
    private readonly ContextRetrievalService _contextRetrievalService;

    public ContextRetrievalServiceTests()
    {
        _mockKnowledgeBaseService = new Mock<IKnowledgeBaseService>();
        _mockQueryProcessingService = new Mock<IQueryProcessingService>();
        _contextRetrievalService = new ContextRetrievalService(
            _mockKnowledgeBaseService.Object,
            _mockQueryProcessingService.Object);
    }

    [Fact]
    public void Constructor_WithNullKnowledgeBaseService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ContextRetrievalService(null!, _mockQueryProcessingService.Object));
    }

    [Fact]
    public void Constructor_WithNullQueryProcessingService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ContextRetrievalService(_mockKnowledgeBaseService.Object, null!));
    }

    [Fact]
    public async Task RetrieveContextAsync_WithValidProcessedQuery_ReturnsSuccessfulResult()
    {
        // Arrange
        var processedQuery = CreateValidProcessedQuery();
        var knowledgeBaseResults = CreateSampleKnowledgeBaseResults();
        
        _mockKnowledgeBaseService
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()))
            .ReturnsAsync(knowledgeBaseResults);

        // Act
        var result = await _contextRetrievalService.RetrieveContextAsync(processedQuery);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Query.Should().Be(processedQuery);
        result.Documents.Should().HaveCountLessOrEqualTo(10); // Default max results
        result.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task RetrieveContextAsync_WithInvalidProcessedQuery_ReturnsFailedResult()
    {
        // Arrange
        var invalidQuery = CreateInvalidProcessedQuery();

        // Act
        var result = await _contextRetrievalService.RetrieveContextAsync(invalidQuery);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid query");
        result.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrieveContextAsync_WithStringQuery_ProcessesQueryAndReturnsResult()
    {
        // Arrange
        var query = "How do I reset my password?";
        var processedQuery = CreateValidProcessedQuery();
        var knowledgeBaseResults = CreateSampleKnowledgeBaseResults();
        
        _mockQueryProcessingService
            .Setup(x => x.ProcessQueryAsync(query))
            .ReturnsAsync(processedQuery);
            
        _mockKnowledgeBaseService
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()))
            .ReturnsAsync(knowledgeBaseResults);

        // Act
        var result = await _contextRetrievalService.RetrieveContextAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        _mockQueryProcessingService.Verify(x => x.ProcessQueryAsync(query), Times.Once);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithCustomParameters_UsesProvidedParameters()
    {
        // Arrange
        var processedQuery = CreateValidProcessedQuery();
        var parameters = new RetrievalParameters
        {
            MaxResults = 5,
            MinSimilarityThreshold = 0.5f
        };
        var knowledgeBaseResults = CreateSampleKnowledgeBaseResults();
        
        _mockKnowledgeBaseService
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()))
            .ReturnsAsync(knowledgeBaseResults);

        // Act
        var result = await _contextRetrievalService.RetrieveContextAsync(processedQuery, parameters);

        // Assert
        result.Documents.Should().HaveCountLessOrEqualTo(5);
        result.Metadata.Parameters.Should().Be(parameters);
        
        // Verify knowledge base was called with correct parameters
        _mockKnowledgeBaseService.Verify(
            x => x.SearchAsync(
                processedQuery.ProcessedText,
                10, // MaxResults * 2 for filtering
                0.5f), // MinSimilarityThreshold
            Times.Once);
    }

    [Fact]
    public async Task RetrieveContextAsync_WhenKnowledgeBaseThrows_ReturnsFailedResult()
    {
        // Arrange
        var processedQuery = CreateValidProcessedQuery();
        
        _mockKnowledgeBaseService
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()))
            .ThrowsAsync(new InvalidOperationException("Knowledge base error"));

        // Act
        var result = await _contextRetrievalService.RetrieveContextAsync(processedQuery);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Context retrieval failed");
        result.ErrorMessage.Should().Contain("Knowledge base error");
    }

    [Fact]
    public void GetDefaultParameters_ReturnsValidDefaultParameters()
    {
        // Act
        var parameters = _contextRetrievalService.GetDefaultParameters();

        // Assert
        parameters.Should().NotBeNull();
        parameters.MaxResults.Should().Be(10);
        parameters.MinSimilarityThreshold.Should().Be(0.1f);
        parameters.IncludeSimilarityScores.Should().BeTrue();
        parameters.IncludeRelevanceReasoning.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithValidParameters_ReturnsValid()
    {
        // Arrange
        var parameters = new RetrievalParameters
        {
            MaxResults = 10,
            MinSimilarityThreshold = 0.5f
        };

        // Act
        var result = _contextRetrievalService.ValidateParameters(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMaxResults_ReturnsInvalid()
    {
        // Arrange
        var parameters = new RetrievalParameters
        {
            MaxResults = 0,
            MinSimilarityThreshold = 0.5f
        };

        // Act
        var result = _contextRetrievalService.ValidateParameters(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MaxResults must be greater than 0");
    }

    [Fact]
    public void ValidateParameters_WithInvalidSimilarityThreshold_ReturnsInvalid()
    {
        // Arrange
        var parameters = new RetrievalParameters
        {
            MaxResults = 10,
            MinSimilarityThreshold = 1.5f // Invalid - greater than 1.0
        };

        // Act
        var result = _contextRetrievalService.ValidateParameters(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MinSimilarityThreshold must be between 0.0 and 1.0");
    }

    [Fact]
    public void ValidateParameters_WithHighSimilarityThreshold_ReturnsWarning()
    {
        // Arrange
        var parameters = new RetrievalParameters
        {
            MaxResults = 10,
            MinSimilarityThreshold = 0.9f
        };

        // Act
        var result = _contextRetrievalService.ValidateParameters(parameters);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.WarningMessage.Should().Contain("Very high similarity threshold");
    }

    [Fact]
    public async Task RetrieveContextAsync_WithContentTypeFilter_FiltersResults()
    {
        // Arrange
        var processedQuery = CreateValidProcessedQuery();
        var parameters = new RetrievalParameters
        {
            ContentTypeFilter = "documentation"
        };
        
        var knowledgeBaseResults = new[]
        {
            CreateKnowledgeBaseResult("Doc content", "source1", 0.8f, 
                new Dictionary<string, object> { ["ContentType"] = "documentation" }),
            CreateKnowledgeBaseResult("FAQ content", "source2", 0.7f, 
                new Dictionary<string, object> { ["ContentType"] = "faq" }),
            CreateKnowledgeBaseResult("Guide content", "source3", 0.6f, 
                new Dictionary<string, object> { ["ContentType"] = "documentation" })
        };
        
        _mockKnowledgeBaseService
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()))
            .ReturnsAsync(knowledgeBaseResults);

        // Act
        var result = await _contextRetrievalService.RetrieveContextAsync(processedQuery, parameters);

        // Assert
        result.Documents.Should().HaveCount(2); // Only documentation items
        result.Documents.Should().OnlyContain(d => 
            d.Document.Metadata!["ContentType"].ToString() == "documentation");
    }

    [Fact]
    public async Task RetrieveContextAsync_ValidQuery_HasCorrectMetadata()
    {
        // Arrange
        var processedQuery = CreateValidProcessedQuery();
        var knowledgeBaseResults = CreateSampleKnowledgeBaseResults();
        
        _mockKnowledgeBaseService
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>()))
            .ReturnsAsync(knowledgeBaseResults);

        // Act
        var result = await _contextRetrievalService.RetrieveContextAsync(processedQuery);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.TotalDocumentsFound.Should().BeGreaterThan(0);
        result.Metadata.DocumentsReturned.Should().BeGreaterOrEqualTo(0);
        result.Metadata.RetrievalTimeMs.Should().BeGreaterOrEqualTo(0);
        result.Metadata.Parameters.Should().NotBeNull();
        
        if (result.Documents.Length > 0)
        {
            result.Metadata.AverageSimilarityScore.Should().BeGreaterThan(0);
            result.Metadata.HighestSimilarityScore.Should().BeGreaterThan(0);
            result.Metadata.LowestSimilarityScore.Should().BeGreaterThan(0);
        }
    }

    private static ProcessedQuery CreateValidProcessedQuery()
    {
        return new ProcessedQuery
        {
            OriginalQuery = "How do I reset my password?",
            ProcessedText = "how do i reset my password",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f },
            IsValid = true,
            ValidationError = null,
            Metadata = new QueryMetadata
            {
                OriginalLength = 26,
                ProcessedLength = 25,
                WordCount = 6,
                ProcessedWordCount = 6,
                EmbeddingDimension = 3,
                ProcessingTimeMs = 10
            },
            ProcessedAt = DateTime.UtcNow
        };
    }

    private static ProcessedQuery CreateInvalidProcessedQuery()
    {
        return new ProcessedQuery
        {
            OriginalQuery = "",
            ProcessedText = "",
            Embedding = Array.Empty<float>(),
            IsValid = false,
            ValidationError = "Query cannot be empty",
            Metadata = new QueryMetadata
            {
                OriginalLength = 0,
                ProcessedLength = 0,
                WordCount = 0,
                ProcessedWordCount = 0,
                EmbeddingDimension = 0,
                ProcessingTimeMs = 5
            },
            ProcessedAt = DateTime.UtcNow
        };
    }

    private static KnowledgeBaseResult[] CreateSampleKnowledgeBaseResults()
    {
        return new[]
        {
            CreateKnowledgeBaseResult("Password reset instructions", "doc1", 0.9f),
            CreateKnowledgeBaseResult("Account security guide", "doc2", 0.7f),
            CreateKnowledgeBaseResult("Login troubleshooting", "doc3", 0.5f)
        };
    }

    private static KnowledgeBaseResult CreateKnowledgeBaseResult(
        string content, 
        string source, 
        float similarity, 
        Dictionary<string, object>? metadata = null)
    {
        return new KnowledgeBaseResult
        {
            Content = content,
            Source = source,
            SimilarityScore = similarity,
            Metadata = metadata,
            ChunkIndex = 0
        };
    }
}
