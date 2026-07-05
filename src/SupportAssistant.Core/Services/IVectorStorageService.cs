using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for storing and retrieving document embeddings with metadata.
/// </summary>
public interface IVectorStorageService
{
    /// <summary>
    /// Initializes the vector storage service.
    /// </summary>
    /// <param name="storagePath">Path where vector data should be stored.</param>
    /// <param name="embeddingDimension">The dimension of embedding vectors to be stored.</param>
    /// <returns>True if initialization was successful, false otherwise.</returns>
    Task<bool> InitializeAsync(string storagePath, int embeddingDimension);

    /// <summary>
    /// Stores a document chunk with its embedding and metadata.
    /// </summary>
    /// <param name="id">Unique identifier for the document chunk.</param>
    /// <param name="content">The text content of the chunk.</param>
    /// <param name="embedding">The embedding vector for the content.</param>
    /// <param name="source">The source of the document (URL, file path, etc.).</param>
    /// <param name="metadata">Additional metadata about the chunk.</param>
    /// <returns>True if storage was successful, false otherwise.</returns>
    Task<bool> StoreAsync(string id, string content, float[] embedding, string source, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Searches for the most similar documents to the given query embedding.
    /// </summary>
    /// <param name="queryEmbedding">The embedding vector of the search query.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="similarityThreshold">Minimum similarity score threshold.</param>
    /// <returns>A list of similar document chunks with similarity scores.</returns>
    Task<IEnumerable<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int maxResults = 5, float similarityThreshold = 0.7f);

    /// <summary>
    /// Gets the total number of stored document chunks.
    /// </summary>
    /// <returns>The count of stored chunks.</returns>
    Task<int> GetChunkCountAsync();

    /// <summary>
    /// Gets aggregate statistics (chunk count, unique document count, and total characters)
    /// maintained incrementally during ingestion. This is an O(1) operation.
    /// </summary>
    /// <returns>A snapshot of the current storage statistics.</returns>
    Task<VectorStorageStatistics> GetStatisticsAsync();

    /// <summary>
    /// Deletes all stored vectors and metadata.
    /// </summary>
    /// <returns>True if clearing was successful, false otherwise.</returns>
    Task<bool> ClearAsync();

    /// <summary>
    /// Optimizes the storage for better search performance.
    /// </summary>
    /// <returns>True if optimization was successful, false otherwise.</returns>
    Task<bool> OptimizeAsync();
}

/// <summary>
/// Represents a result from vector similarity search.
/// </summary>
public class VectorSearchResult
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required string Source { get; init; }
    public required float SimilarityScore { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Aggregate statistics for the vector storage, maintained incrementally so reads are O(1).
/// </summary>
public class VectorStorageStatistics
{
    /// <summary>Total number of stored document chunks.</summary>
    public int ChunkCount { get; init; }

    /// <summary>Number of distinct document sources.</summary>
    public int DocumentCount { get; init; }

    /// <summary>Total character count across all stored chunks.</summary>
    public long TotalCharacters { get; init; }
}
