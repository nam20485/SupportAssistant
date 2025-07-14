using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for managing knowledge base operations including document ingestion,
/// text processing, embedding generation, and similarity search.
/// </summary>
public interface IKnowledgeBaseService
{
    /// <summary>
    /// Initializes the knowledge base service and prepares the vector storage.
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise.</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Ingests a document into the knowledge base by processing, chunking, and storing it.
    /// </summary>
    /// <param name="content">The raw text content of the document.</param>
    /// <param name="source">The source identifier (URL, file path, etc.).</param>
    /// <param name="metadata">Additional metadata about the document.</param>
    /// <returns>True if ingestion was successful, false otherwise.</returns>
    Task<bool> IngestDocumentAsync(string content, string source, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Searches the knowledge base for documents similar to the query.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="maxResults">Maximum number of results to return (default: 5).</param>
    /// <param name="similarityThreshold">Minimum similarity score threshold (default: 0.7).</param>
    /// <returns>A list of relevant document chunks with similarity scores.</returns>
    Task<IEnumerable<KnowledgeBaseResult>> SearchAsync(string query, int maxResults = 5, float similarityThreshold = 0.7f);

    /// <summary>
    /// Gets the current statistics of the knowledge base.
    /// </summary>
    /// <returns>Knowledge base statistics including document count, chunk count, etc.</returns>
    Task<KnowledgeBaseStatistics> GetStatisticsAsync();
}

/// <summary>
/// Represents a search result from the knowledge base.
/// </summary>
public class KnowledgeBaseResult
{
    public required string Content { get; init; }
    public required string Source { get; init; }
    public required float SimilarityScore { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public int ChunkIndex { get; init; }
}

/// <summary>
/// Statistics about the current state of the knowledge base.
/// </summary>
public class KnowledgeBaseStatistics
{
    public int DocumentCount { get; init; }
    public int ChunkCount { get; init; }
    public long TotalCharacters { get; init; }
    public DateTime LastUpdated { get; init; }
    public string StorageLocation { get; init; } = string.Empty;
}
