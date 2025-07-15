using System.Threading.Tasks;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for generating AI-powered responses to user queries using context from the knowledge base
/// </summary>
public interface IResponseGenerationService
{
    /// <summary>
    /// Generates a response using the provided request containing query and context
    /// </summary>
    /// <param name="request">Complete request with query, context, and generation parameters</param>
    /// <returns>Generated response with metadata and source references</returns>
    Task<GeneratedResponse> GenerateResponseAsync(ResponseGenerationRequest request);
    
    /// <summary>
    /// Generates a response using a query and context with default parameters
    /// </summary>
    /// <param name="query">The processed user query</param>
    /// <param name="context">Retrieved context from the knowledge base</param>
    /// <param name="parameters">Optional generation parameters (uses defaults if null)</param>
    /// <returns>Generated response with metadata and source references</returns>
    Task<GeneratedResponse> GenerateResponseAsync(
        ProcessedQuery query, 
        ContextRetrievalResult context, 
        Models.ResponseGenerationParameters? parameters = null);
    
    /// <summary>
    /// Generates a response from a raw query string (will process query and retrieve context automatically)
    /// </summary>
    /// <param name="query">Raw user query string</param>
    /// <param name="parameters">Optional generation parameters (uses defaults if null)</param>
    /// <returns>Generated response with metadata and source references</returns>
    Task<GeneratedResponse> GenerateResponseAsync(
        string query, 
        Models.ResponseGenerationParameters? parameters = null);
    
    /// <summary>
    /// Gets the default response generation parameters
    /// </summary>
    /// <returns>Default parameters optimized for support assistant responses</returns>
    Models.ResponseGenerationParameters GetDefaultParameters();
    
    /// <summary>
    /// Validates the provided response generation parameters
    /// </summary>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>Validation result indicating if parameters are valid</returns>
    ResponseGenerationParametersValidationResult ValidateParameters(Models.ResponseGenerationParameters parameters);
    
    /// <summary>
    /// Gets information about the language model being used
    /// </summary>
    /// <returns>Model information and capabilities</returns>
    Task<LanguageModelInfo> GetModelInfoAsync();
}

/// <summary>
/// Result of response generation parameters validation
/// </summary>
public class ResponseGenerationParametersValidationResult
{
    /// <summary>
    /// Whether the parameters are valid
    /// </summary>
    public required bool IsValid { get; init; }
    
    /// <summary>
    /// Error message if parameters are invalid
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Warning message for sub-optimal but valid parameters
    /// </summary>
    public string? WarningMessage { get; init; }
    
    /// <summary>
    /// List of specific validation issues found
    /// </summary>
    public string[]? Issues { get; init; }
}

/// <summary>
/// Information about the language model being used for response generation
/// </summary>
public class LanguageModelInfo
{
    /// <summary>
    /// Name/identifier of the model
    /// </summary>
    public required string ModelName { get; init; }
    
    /// <summary>
    /// Version of the model
    /// </summary>
    public string? ModelVersion { get; init; }
    
    /// <summary>
    /// Maximum context length supported by the model
    /// </summary>
    public int MaxContextLength { get; init; }
    
    /// <summary>
    /// Maximum output length supported by the model
    /// </summary>
    public int MaxOutputLength { get; init; }
    
    /// <summary>
    /// Whether the model is currently available for use
    /// </summary>
    public required bool IsAvailable { get; init; }
    
    /// <summary>
    /// Error message if model is not available
    /// </summary>
    public string? UnavailableReason { get; init; }
    
    /// <summary>
    /// Model capabilities and features
    /// </summary>
    public string[]? Capabilities { get; init; }
    
    /// <summary>
    /// Recommended parameters for this model
    /// </summary>
    public Models.ResponseGenerationParameters? RecommendedParameters { get; init; }
}
