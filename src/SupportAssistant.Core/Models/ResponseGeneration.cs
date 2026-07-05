using System;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Core.Models;

/// <summary>
/// Request for generating a response using AI language model
/// </summary>
public class ResponseGenerationRequest
{
    /// <summary>
    /// The processed user query
    /// </summary>
    public required ProcessedQuery Query { get; init; }
    
    /// <summary>
    /// Context retrieved from the knowledge base
    /// </summary>
    public required ContextRetrievalResult Context { get; init; }
    
    /// <summary>
    /// Parameters for response generation
    /// </summary>
    public required ResponseGenerationParameters Parameters { get; init; }
}

/// <summary>
/// Generated response from the AI language model
/// </summary>
public class GeneratedResponse
{
    /// <summary>
    /// The generated response text
    /// </summary>
    public required string ResponseText { get; init; }
    
    /// <summary>
    /// The original processed query
    /// </summary>
    public required ProcessedQuery Query { get; init; }
    
    /// <summary>
    /// Context that was used for generation
    /// </summary>
    public required ContextRetrievalResult Context { get; init; }
    
    /// <summary>
    /// Confidence score for the generated response (0.0 to 1.0)
    /// </summary>
    public required float Confidence { get; init; }
    
    /// <summary>
    /// References to source documents used in the response
    /// </summary>
    public required ResponseSourceReference[] SourceReferences { get; init; }
    
    /// <summary>
    /// Metadata about the response generation process
    /// </summary>
    public required ResponseMetadata Metadata { get; init; }
}

/// <summary>
/// Parameters controlling response generation behavior
/// </summary>
public class ResponseGenerationParameters
{
    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    public required int MaxTokens { get; init; }
    
    /// <summary>
    /// Temperature for controlling randomness (0.0 = deterministic, higher = more creative)
    /// </summary>
    public required float Temperature { get; init; }
    
    /// <summary>
    /// Top-p sampling parameter for nucleus sampling
    /// </summary>
    public required float TopP { get; init; }
    
    /// <summary>
    /// Top-k sampling parameter
    /// </summary>
    public required int TopK { get; init; }
    
    /// <summary>
    /// Repetition penalty to discourage repetitive text
    /// </summary>
    public required float RepetitionPenalty { get; init; }
    
    /// <summary>
    /// System prompt to guide the AI's behavior
    /// </summary>
    public required string SystemPrompt { get; init; }
    
    /// <summary>
    /// Whether to include source references in the response
    /// </summary>
    public required bool IncludeSourceReferences { get; init; }
    
    /// <summary>
    /// Whether to require attribution to source documents
    /// </summary>
    public required bool RequireSourceAttribution { get; init; }
    
    /// <summary>
    /// Maximum number of tokens to use from context
    /// </summary>
    public required int MaxContextTokens { get; init; }
}

/// <summary>
/// Metadata about the response generation process
/// </summary>
public class ResponseMetadata
{
    /// <summary>
    /// When the response was generated
    /// </summary>
    public required DateTime GeneratedAt { get; init; }
    
    /// <summary>
    /// Time taken to generate the response in milliseconds
    /// </summary>
    public required int ProcessingTimeMs { get; init; }
    
    /// <summary>
    /// Model used for generation
    /// </summary>
    public required string ModelUsed { get; init; }
    
    /// <summary>
    /// Parameters used for generation
    /// </summary>
    public required ResponseGenerationParameters Parameters { get; init; }
    
    /// <summary>
    /// Number of tokens generated
    /// </summary>
    public required int TokensGenerated { get; init; }
    
    /// <summary>
    /// Number of tokens in the prompt
    /// </summary>
    public required int PromptTokens { get; init; }
    
    /// <summary>
    /// Whether the response has high confidence
    /// </summary>
    public required bool HasHighConfidence { get; init; }
    
    /// <summary>
    /// Whether a fallback response was used due to error
    /// </summary>
    public required bool UsedFallback { get; init; }
    
    /// <summary>
    /// Error message if generation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Reference to a source document used in response generation
/// </summary>
public class ResponseSourceReference
{
    /// <summary>
    /// Path to the source document
    /// </summary>
    public required string DocumentPath { get; init; }
    
    /// <summary>
    /// Relevance score of this source to the query
    /// </summary>
    public required float RelevanceScore { get; init; }
    
    /// <summary>
    /// Specific section or excerpt used from the document
    /// </summary>
    public required string SectionUsed { get; init; }
    
    /// <summary>
    /// Confidence in how well this source was used in the response
    /// </summary>
    public required float ConfidenceInUsage { get; init; }
}
