using System.IO;
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

public class FileVectorStorageServiceTests
{
    private readonly string _testStoragePath = Path.Combine(Path.GetTempPath(), "test_vectors.json");

    [Fact]
    public async Task InitializeAsync_ValidParameters_ReturnsTrue()
    {
        // Arrange
        var service = new FileVectorStorageService();

        // Act
        var result = await service.InitializeAsync(_testStoragePath, 384);

        // Assert
        result.Should().BeTrue();

        // Cleanup
        if (File.Exists(_testStoragePath))
            File.Delete(_testStoragePath);
    }

    [Fact]
    public async Task StoreAsync_ValidVector_ReturnsTrue()
    {
        // Arrange
        var service = new FileVectorStorageService();
        await service.InitializeAsync(_testStoragePath, 3);
        var embedding = new[] { 0.1f, 0.2f, 0.3f };

        // Act
        var result = await service.StoreAsync("test1", "test content", embedding, "test_source");

        // Assert
        result.Should().BeTrue();
        var count = await service.GetChunkCountAsync();
        count.Should().Be(1);

        // Cleanup
        if (File.Exists(_testStoragePath))
            File.Delete(_testStoragePath);
    }

    [Fact]
    public async Task SearchAsync_SimilarVector_ReturnsResults()
    {
        // Arrange
        var service = new FileVectorStorageService();
        await service.InitializeAsync(_testStoragePath, 3);
        var embedding = new[] { 1.0f, 0.0f, 0.0f };
        await service.StoreAsync("test1", "test content", embedding, "test_source");

        // Act
        var queryEmbedding = new[] { 0.9f, 0.1f, 0.0f }; // Similar to stored vector
        var results = (await service.SearchAsync(queryEmbedding, maxResults: 5, similarityThreshold: 0.5f)).ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.First().Content.Should().Be("test content");

        // Cleanup
        if (File.Exists(_testStoragePath))
            File.Delete(_testStoragePath);
    }
}

public class SimpleEmbeddingServiceTests
{
    [Fact]
    public async Task InitializeAsync_ReturnsTrue()
    {
        // Arrange
        var service = new SimpleEmbeddingService();

        // Act
        var result = await service.InitializeAsync("");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_ReturnsEmbedding()
    {
        // Arrange
        var service = new SimpleEmbeddingService();
        await service.InitializeAsync("");

        // Act
        var embedding = await service.GenerateEmbeddingAsync("test text");

        // Assert
        embedding.Should().HaveCount(384);
        embedding.Should().NotContain(float.NaN);
        embedding.Should().NotContain(float.PositiveInfinity);
        embedding.Should().NotContain(float.NegativeInfinity);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SameText_ReturnsSameEmbedding()
    {
        // Arrange
        var service = new SimpleEmbeddingService();
        await service.InitializeAsync("");

        // Act
        var embedding1 = await service.GenerateEmbeddingAsync("test text");
        var embedding2 = await service.GenerateEmbeddingAsync("test text");

        // Assert
        embedding1.Should().Equal(embedding2);
    }

    [Fact]
    public void CalculateCosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        // Arrange
        var service = new SimpleEmbeddingService();
        var vector = new[] { 1.0f, 0.0f, 0.0f };

        // Act
        var similarity = service.CalculateCosineSimilarity(vector, vector);

        // Assert
        similarity.Should().BeApproximately(1.0f, 0.001f);
    }
}

public class KnowledgeBaseServiceTests
{
    [Fact]
    public async Task InitializeAsync_ValidServices_ReturnsTrue()
    {
        // Arrange
        var chunkingService = new TextChunkingService();
        var embeddingService = new SimpleEmbeddingService();
        var vectorStorageService = new FileVectorStorageService();
        var knowledgeBaseService = new KnowledgeBaseService(chunkingService, embeddingService, vectorStorageService);

        // Act
        var result = await knowledgeBaseService.InitializeAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IngestDocumentAsync_ValidDocument_ReturnsTrue()
    {
        // Arrange
        var chunkingService = new TextChunkingService();
        var embeddingService = new SimpleEmbeddingService();
        var vectorStorageService = new FileVectorStorageService();
        var knowledgeBaseService = new KnowledgeBaseService(chunkingService, embeddingService, vectorStorageService);
        await knowledgeBaseService.InitializeAsync();

        // Act
        var result = await knowledgeBaseService.IngestDocumentAsync("This is a test document with some content.", "test_doc.txt");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_AfterIngestion_ReturnsResults()
    {
        // Arrange
        var chunkingService = new TextChunkingService();
        var embeddingService = new SimpleEmbeddingService();
        var vectorStorageService = new FileVectorStorageService();
        var knowledgeBaseService = new KnowledgeBaseService(chunkingService, embeddingService, vectorStorageService);
        await knowledgeBaseService.InitializeAsync();
        await knowledgeBaseService.IngestDocumentAsync("This is a test document about programming.", "test_doc.txt");

        // Act
        var results = await knowledgeBaseService.SearchAsync("programming", maxResults: 5, similarityThreshold: 0.01f);

        // Assert
        results.Should().NotBeEmpty();
    }
}
