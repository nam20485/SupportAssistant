using System;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Core.Models;

/// <summary>
/// Represents a document retrieved from the context retrieval system
/// </summary>
public class RetrievedDocument
{
    /// <summary>
    /// The original document result from the knowledge base
    /// </summary>
    public required KnowledgeBaseResult Document { get; init; }
    
    /// <summary>
    /// Similarity score between the query and this document (0.0 to 1.0)
    /// </summary>
    public required float SimilarityScore { get; init; }
    
    /// <summary>
    /// Ranking position in the retrieved results (1-based)
    /// </summary>
    public required int Rank { get; init; }
    
    /// <summary>
    /// Relevance explanation or reasoning for why this document was selected
    /// </summary>
    public string? RelevanceReason { get; init; }
    
    /// <summary>
    /// Timestamp when this document was retrieved
    /// </summary>
    public DateTime RetrievedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Parameters for configuring context retrieval behavior
/// </summary>
public class RetrievalParameters
{
    /// <summary>
    /// Maximum number of documents to retrieve (default: 10)
    /// </summary>
    public int MaxResults { get; init; } = 10;
    
    /// <summary>
    /// Minimum similarity threshold for including documents (0.0 to 1.0, default: 0.1)
    /// </summary>
    public float MinSimilarityThreshold { get; init; } = 0.1f;
    
    /// <summary>
    /// Whether to include similarity scores in results (default: true)
    /// </summary>
    public bool IncludeSimilarityScores { get; init; } = true;
    
    /// <summary>
    /// Whether to include relevance reasoning (default: false, as it's computationally expensive)
    /// </summary>
    public bool IncludeRelevanceReasoning { get; init; } = false;
    
    /// <summary>
    /// Content type filter (e.g., "documentation", "faq", null for all types)
    /// </summary>
    public string? ContentTypeFilter { get; init; }
    
    /// <summary>
    /// Tags to filter by (documents must have at least one of these tags)
    /// </summary>
    public string[]? TagsFilter { get; init; }
    
    /// <summary>
    /// Minimum document length in characters (null for no minimum)
    /// </summary>
    public int? MinDocumentLength { get; init; }
    
    /// <summary>
    /// Maximum document length in characters (null for no maximum)
    /// </summary>
    public int? MaxDocumentLength { get; init; }
}

/// <summary>
/// Metadata about the context retrieval operation
/// </summary>
public class RetrievalMetadata
{
    /// <summary>
    /// Total number of documents found before filtering
    /// </summary>
    public required int TotalDocumentsFound { get; init; }
    
    /// <summary>
    /// Number of documents returned after filtering
    /// </summary>
    public required int DocumentsReturned { get; init; }
    
    /// <summary>
    /// Time taken for the retrieval operation in milliseconds
    /// </summary>
    public required long RetrievalTimeMs { get; init; }
    
    /// <summary>
    /// The retrieval parameters used for this operation
    /// </summary>
    public required RetrievalParameters Parameters { get; init; }
    
    /// <summary>
    /// Whether any documents were filtered out due to similarity threshold
    /// </summary>
    public required bool DocumentsFilteredBySimilarity { get; init; }
    
    /// <summary>
    /// Whether any documents were filtered out due to content filters
    /// </summary>
    public required bool DocumentsFilteredByContent { get; init; }
    
    /// <summary>
    /// Average similarity score of returned documents
    /// </summary>
    public float? AverageSimilarityScore { get; init; }
    
    /// <summary>
    /// Highest similarity score among returned documents
    /// </summary>
    public float? HighestSimilarityScore { get; init; }
    
    /// <summary>
    /// Lowest similarity score among returned documents
    /// </summary>
    public float? LowestSimilarityScore { get; init; }
}

/// <summary>
/// Result of a context retrieval operation
/// </summary>
public class ContextRetrievalResult
{
    /// <summary>
    /// The processed query used for retrieval
    /// </summary>
    public required ProcessedQuery Query { get; init; }
    
    /// <summary>
    /// Retrieved documents ordered by relevance/similarity
    /// </summary>
    public required RetrievedDocument[] Documents { get; init; }
    
    /// <summary>
    /// Whether the retrieval operation was successful
    /// </summary>
    public required bool IsSuccessful { get; init; }
    
    /// <summary>
    /// Error message if the retrieval operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Metadata about the retrieval operation
    /// </summary>
    public required RetrievalMetadata Metadata { get; init; }
    
    /// <summary>
    /// Timestamp when the retrieval was performed
    /// </summary>
    public DateTime RetrievedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets whether any relevant documents were found
    /// </summary>
    public bool HasResults => Documents.Length > 0;
    
    /// <summary>
    /// Gets the most relevant document (highest similarity score) or null if no results
    /// </summary>
    public RetrievedDocument? MostRelevantDocument => 
        Documents.Length > 0 ? Documents[0] : null;
}
