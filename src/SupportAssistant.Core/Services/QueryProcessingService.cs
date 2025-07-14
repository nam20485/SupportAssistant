using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Implementation of query processing service with validation, preprocessing, and embedding generation
/// </summary>
public class QueryProcessingService : IQueryProcessingService
{
    private readonly IEmbeddingService _embeddingService;
    
    // Configuration constants
    private const int MinQueryLength = 1;
    private const int MaxQueryLength = 1000;
    private const int MinMeaningfulLength = 2;
    
    // Regex for invalid characters (adjust as needed)
    private static readonly Regex InvalidCharsRegex = new(@"[^\w\s\-.,!?;:()\[\]""']+", RegexOptions.Compiled);
    
    public QueryProcessingService(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
    }

    public async Task<ProcessedQuery> ProcessQueryAsync(string query)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate the query first
            var validationResult = GetValidationResult(query);
            
            if (!validationResult.IsValid)
            {
                return new ProcessedQuery
                {
                    OriginalQuery = query ?? string.Empty,
                    ProcessedText = string.Empty,
                    Embedding = Array.Empty<float>(),
                    IsValid = false,
                    ValidationError = validationResult.ErrorMessage,
                    Metadata = CreateMetadata(query, string.Empty, 0, stopwatch.ElapsedMilliseconds),
                    ProcessedAt = DateTime.UtcNow
                };
            }
            
            // Preprocess the query
            var processedText = PreprocessQuery(query);
            
            // Generate embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(processedText);
            
            stopwatch.Stop();
            
            return new ProcessedQuery
            {
                OriginalQuery = query,
                ProcessedText = processedText,
                Embedding = embedding,
                IsValid = true,
                ValidationError = null,
                Metadata = CreateMetadata(query, processedText, embedding.Length, stopwatch.ElapsedMilliseconds),
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            return new ProcessedQuery
            {
                OriginalQuery = query ?? string.Empty,
                ProcessedText = string.Empty,
                Embedding = Array.Empty<float>(),
                IsValid = false,
                ValidationError = $"Query processing failed: {ex.Message}",
                Metadata = CreateMetadata(query, string.Empty, 0, stopwatch.ElapsedMilliseconds),
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    public bool ValidateQuery(string query)
    {
        return GetValidationResult(query).IsValid;
    }

    public QueryValidationResult GetValidationResult(string query)
    {
        var issues = new List<QueryValidationIssue>();
        
        // Check for null or empty
        if (string.IsNullOrEmpty(query))
        {
            issues.Add(new QueryValidationIssue
            {
                Type = QueryValidationIssueType.EmptyQuery,
                Message = "Query cannot be null or empty",
                Severity = QueryValidationSeverity.Error
            });
            
            return new QueryValidationResult
            {
                IsValid = false,
                ErrorMessage = "Query cannot be null or empty",
                Issues = issues.ToArray()
            };
        }
        
        // Check for whitespace only
        if (string.IsNullOrWhiteSpace(query))
        {
            issues.Add(new QueryValidationIssue
            {
                Type = QueryValidationIssueType.WhitespaceOnly,
                Message = "Query cannot contain only whitespace",
                Severity = QueryValidationSeverity.Error
            });
        }
        
        // Check length constraints
        if (query.Length < MinQueryLength)
        {
            issues.Add(new QueryValidationIssue
            {
                Type = QueryValidationIssueType.TooShort,
                Message = $"Query must be at least {MinQueryLength} character(s) long",
                Severity = QueryValidationSeverity.Error
            });
        }
        
        if (query.Length > MaxQueryLength)
        {
            issues.Add(new QueryValidationIssue
            {
                Type = QueryValidationIssueType.TooLong,
                Message = $"Query cannot exceed {MaxQueryLength} characters",
                Severity = QueryValidationSeverity.Error
            });
        }
        
        // Check for meaningful content
        var trimmedQuery = query.Trim();
        if (trimmedQuery.Length > 0 && trimmedQuery.Length < MinMeaningfulLength)
        {
            issues.Add(new QueryValidationIssue
            {
                Type = QueryValidationIssueType.TooShort,
                Message = $"Query must be at least {MinMeaningfulLength} meaningful characters",
                Severity = QueryValidationSeverity.Warning
            });
        }
        
        // Check for invalid characters
        if (InvalidCharsRegex.IsMatch(query))
        {
            issues.Add(new QueryValidationIssue
            {
                Type = QueryValidationIssueType.InvalidCharacters,
                Message = "Query contains unsupported characters",
                Severity = QueryValidationSeverity.Warning
            });
        }
        
        // Check for low quality (very repetitive content)
        if (IsLowQualityQuery(query))
        {
            issues.Add(new QueryValidationIssue
            {
                Type = QueryValidationIssueType.LowQuality,
                Message = "Query appears to be low quality or repetitive",
                Severity = QueryValidationSeverity.Warning
            });
        }
        
        // Determine if valid (no errors, warnings are ok)
        var hasErrors = issues.Any(i => i.Severity == QueryValidationSeverity.Error);
        
        return new QueryValidationResult
        {
            IsValid = !hasErrors,
            ErrorMessage = hasErrors ? issues.First(i => i.Severity == QueryValidationSeverity.Error).Message : null,
            Issues = issues.ToArray()
        };
    }

    public string PreprocessQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;
        
        // Basic normalization
        var processed = query.Trim();
        
        // Normalize whitespace
        processed = Regex.Replace(processed, @"\s+", " ");
        
        // Convert to lowercase for consistency (embedding models usually expect this)
        processed = processed.ToLowerInvariant();
        
        // Remove or replace invalid characters with spaces
        processed = InvalidCharsRegex.Replace(processed, " ");
        
        // Final cleanup
        processed = Regex.Replace(processed, @"\s+", " ").Trim();
        
        return processed;
    }
    
    private static bool IsLowQualityQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        
        // Check for excessive repetition of characters
        var charGroups = query.GroupBy(c => c);
        foreach (var group in charGroups)
        {
            if (group.Count() > query.Length * 0.6) // More than 60% same character
                return true;
        }
        
        // Check for excessive repetition of words
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 3)
        {
            var wordGroups = words.GroupBy(w => w);
            foreach (var group in wordGroups)
            {
                if (group.Count() > words.Length * 0.5) // More than 50% same word
                    return true;
            }
        }
        
        return false;
    }
    
    private static QueryMetadata CreateMetadata(string? originalQuery, string processedQuery, int embeddingDimension, long processingTimeMs)
    {
        originalQuery ??= string.Empty;
        
        var originalWords = string.IsNullOrWhiteSpace(originalQuery) 
            ? Array.Empty<string>() 
            : originalQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
        var processedWords = string.IsNullOrWhiteSpace(processedQuery) 
            ? Array.Empty<string>() 
            : processedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        return new QueryMetadata
        {
            OriginalLength = originalQuery.Length,
            ProcessedLength = processedQuery.Length,
            WordCount = originalWords.Length,
            ProcessedWordCount = processedWords.Length,
            EmbeddingDimension = embeddingDimension,
            ProcessingTimeMs = processingTimeMs
        };
    }
}
