using System.Threading.Tasks;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for retrieving relevant context from the knowledge base based on user queries
/// </summary>
public interface IContextRetrievalService
{
    /// <summary>
    /// Retrieves relevant context from the knowledge base using a processed query
    /// </summary>
    /// <param name="query">The processed query to search for</param>
    /// <param name="parameters">Optional retrieval parameters to customize the search</param>
    /// <returns>Context retrieval result with relevant documents and metadata</returns>
    Task<ContextRetrievalResult> RetrieveContextAsync(ProcessedQuery query, RetrievalParameters? parameters = null);
    
    /// <summary>
    /// Retrieves relevant context from the knowledge base using a raw query string
    /// </summary>
    /// <param name="query">The raw query string to search for</param>
    /// <param name="parameters">Optional retrieval parameters to customize the search</param>
    /// <returns>Context retrieval result with relevant documents and metadata</returns>
    Task<ContextRetrievalResult> RetrieveContextAsync(string query, RetrievalParameters? parameters = null);
    
    /// <summary>
    /// Gets the default retrieval parameters recommended for general use
    /// </summary>
    /// <returns>Default retrieval parameters</returns>
    RetrievalParameters GetDefaultParameters();
    
    /// <summary>
    /// Validates the provided retrieval parameters
    /// </summary>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>Validation result indicating if parameters are valid</returns>
    RetrievalParametersValidationResult ValidateParameters(RetrievalParameters parameters);
}

/// <summary>
/// Result of retrieval parameters validation
/// </summary>
public class RetrievalParametersValidationResult
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
