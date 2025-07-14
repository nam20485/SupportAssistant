using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for processing and validating user queries before search operations
/// </summary>
public interface IQueryProcessingService
{
    /// <summary>
    /// Processes a raw query string into a structured, validated query with embeddings
    /// </summary>
    /// <param name="query">The raw query text from the user</param>
    /// <returns>A processed query with embeddings and validation results</returns>
    Task<ProcessedQuery> ProcessQueryAsync(string query);
    
    /// <summary>
    /// Validates a query without generating embeddings (faster validation)
    /// </summary>
    /// <param name="query">The query text to validate</param>
    /// <returns>True if the query is valid, false otherwise</returns>
    bool ValidateQuery(string query);
    
    /// <summary>
    /// Gets detailed validation results for a query
    /// </summary>
    /// <param name="query">The query text to validate</param>
    /// <returns>Validation result with error details if invalid</returns>
    QueryValidationResult GetValidationResult(string query);
    
    /// <summary>
    /// Preprocesses query text (normalization, cleanup) without generating embeddings
    /// </summary>
    /// <param name="query">The raw query text</param>
    /// <returns>The preprocessed query text</returns>
    string PreprocessQuery(string query);
}

/// <summary>
/// Result of query validation
/// </summary>
public class QueryValidationResult
{
    /// <summary>
    /// Whether the query is valid
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// Error message if the query is invalid
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Specific validation issues found
    /// </summary>
    public QueryValidationIssue[]? Issues { get; init; }
}

/// <summary>
/// Specific validation issue with a query
/// </summary>
public class QueryValidationIssue
{
    /// <summary>
    /// Type of validation issue
    /// </summary>
    public QueryValidationIssueType Type { get; init; }
    
    /// <summary>
    /// Description of the issue
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Severity of the issue
    /// </summary>
    public QueryValidationSeverity Severity { get; init; }
}

/// <summary>
/// Types of query validation issues
/// </summary>
public enum QueryValidationIssueType
{
    /// <summary>
    /// Query is empty or null
    /// </summary>
    EmptyQuery,
    
    /// <summary>
    /// Query is too short to be meaningful
    /// </summary>
    TooShort,
    
    /// <summary>
    /// Query exceeds maximum length
    /// </summary>
    TooLong,
    
    /// <summary>
    /// Query contains only whitespace
    /// </summary>
    WhitespaceOnly,
    
    /// <summary>
    /// Query contains invalid characters
    /// </summary>
    InvalidCharacters,
    
    /// <summary>
    /// Query appears to be spam or low quality
    /// </summary>
    LowQuality
}

/// <summary>
/// Severity of validation issues
/// </summary>
public enum QueryValidationSeverity
{
    /// <summary>
    /// Information only, query can still be processed
    /// </summary>
    Info,
    
    /// <summary>
    /// Warning, query can be processed but may have issues
    /// </summary>
    Warning,
    
    /// <summary>
    /// Error, query cannot be processed as-is
    /// </summary>
    Error
}
