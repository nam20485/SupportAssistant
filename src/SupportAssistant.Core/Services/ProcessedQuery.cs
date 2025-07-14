using System;
using System.Collections.Generic;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Represents a processed query with embeddings and metadata
/// </summary>
public class ProcessedQuery
{
    /// <summary>
    /// The original raw query text as provided by the user
    /// </summary>
    public string OriginalQuery { get; init; } = string.Empty;
    
    /// <summary>
    /// The processed and normalized query text
    /// </summary>
    public string ProcessedText { get; init; } = string.Empty;
    
    /// <summary>
    /// The embedding vector generated for this query
    /// </summary>
    public float[] Embedding { get; init; } = Array.Empty<float>();
    
    /// <summary>
    /// Indicates whether the query passed validation
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// Validation error message if the query is invalid
    /// </summary>
    public string? ValidationError { get; init; }
    
    /// <summary>
    /// Metadata about the query processing
    /// </summary>
    public QueryMetadata Metadata { get; init; } = new();
    
    /// <summary>
    /// Timestamp when the query was processed
    /// </summary>
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Metadata about a processed query
/// </summary>
public class QueryMetadata
{
    /// <summary>
    /// Length of the original query in characters
    /// </summary>
    public int OriginalLength { get; init; }
    
    /// <summary>
    /// Length of the processed query in characters
    /// </summary>
    public int ProcessedLength { get; init; }
    
    /// <summary>
    /// Number of words in the original query
    /// </summary>
    public int WordCount { get; init; }
    
    /// <summary>
    /// Number of words in the processed query
    /// </summary>
    public int ProcessedWordCount { get; init; }
    
    /// <summary>
    /// Dimension of the generated embedding
    /// </summary>
    public int EmbeddingDimension { get; init; }
    
    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; init; }
    
    /// <summary>
    /// Additional custom metadata
    /// </summary>
    public Dictionary<string, object> CustomMetadata { get; init; } = new();
}
