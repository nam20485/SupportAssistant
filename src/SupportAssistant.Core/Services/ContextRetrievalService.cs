using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Implementation of context retrieval service that uses knowledge base and query processing
/// </summary>
public class ContextRetrievalService : IContextRetrievalService
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IQueryProcessingService _queryProcessingService;

    public ContextRetrievalService(
        IKnowledgeBaseService knowledgeBaseService,
        IQueryProcessingService queryProcessingService)
    {
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _queryProcessingService = queryProcessingService ?? throw new ArgumentNullException(nameof(queryProcessingService));
    }

    public async Task<ContextRetrievalResult> RetrieveContextAsync(ProcessedQuery query, RetrievalParameters? parameters = null)
    {
        parameters ??= GetDefaultParameters();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate query
            if (!query.IsValid)
            {
                return CreateFailedResult(query, parameters, "Invalid query provided", stopwatch.ElapsedMilliseconds);
            }
            
            // Validate parameters
            var paramValidation = ValidateParameters(parameters);
            if (!paramValidation.IsValid)
            {
                return CreateFailedResult(query, parameters, $"Invalid parameters: {paramValidation.ErrorMessage}", stopwatch.ElapsedMilliseconds);
            }
            
            // Search knowledge base
            var searchResults = await _knowledgeBaseService.SearchAsync(
                query.ProcessedText,
                parameters.MaxResults * 2, // Get more results to allow for filtering
                parameters.MinSimilarityThreshold);
            
            var allResults = searchResults.ToArray();
            
            // Apply content filters
            var filteredResults = ApplyContentFilters(allResults, parameters);
            
            // Convert to retrieved documents and rank
            var retrievedDocuments = ConvertToRetrievedDocuments(filteredResults, parameters);
            
            // Limit to requested number of results
            var finalResults = retrievedDocuments.Take(parameters.MaxResults).ToArray();
            
            stopwatch.Stop();
            
            // Create metadata
            var metadata = CreateRetrievalMetadata(
                allResults.Length,
                finalResults.Length,
                stopwatch.ElapsedMilliseconds,
                parameters,
                allResults.Length != filteredResults.Count(),
                filteredResults.Count() != finalResults.Length,
                finalResults);
            
            return new ContextRetrievalResult
            {
                Query = query,
                Documents = finalResults,
                IsSuccessful = true,
                ErrorMessage = null,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateFailedResult(query, parameters, $"Context retrieval failed: {ex.Message}", stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<ContextRetrievalResult> RetrieveContextAsync(string query, RetrievalParameters? parameters = null)
    {
        // Process the raw query first
        var processedQuery = await _queryProcessingService.ProcessQueryAsync(query);
        
        // Use the main retrieval method
        return await RetrieveContextAsync(processedQuery, parameters);
    }

    public RetrievalParameters GetDefaultParameters()
    {
        return new RetrievalParameters
        {
            MaxResults = 10,
            MinSimilarityThreshold = 0.1f,
            IncludeSimilarityScores = true,
            IncludeRelevanceReasoning = false,
            ContentTypeFilter = null,
            TagsFilter = null,
            MinDocumentLength = null,
            MaxDocumentLength = null
        };
    }

    public RetrievalParametersValidationResult ValidateParameters(RetrievalParameters parameters)
    {
        var issues = new List<string>();
        
        // Validate MaxResults
        if (parameters.MaxResults <= 0)
        {
            issues.Add("MaxResults must be greater than 0");
        }
        else if (parameters.MaxResults > 100)
        {
            issues.Add("MaxResults should not exceed 100 for performance reasons");
        }
        
        // Validate similarity threshold
        if (parameters.MinSimilarityThreshold < 0.0f || parameters.MinSimilarityThreshold > 1.0f)
        {
            issues.Add("MinSimilarityThreshold must be between 0.0 and 1.0");
        }
        
        // Validate document length constraints
        if (parameters.MinDocumentLength.HasValue && parameters.MinDocumentLength.Value < 0)
        {
            issues.Add("MinDocumentLength must be non-negative");
        }
        
        if (parameters.MaxDocumentLength.HasValue && parameters.MaxDocumentLength.Value < 0)
        {
            issues.Add("MaxDocumentLength must be non-negative");
        }
        
        if (parameters.MinDocumentLength.HasValue && 
            parameters.MaxDocumentLength.HasValue && 
            parameters.MinDocumentLength.Value > parameters.MaxDocumentLength.Value)
        {
            issues.Add("MinDocumentLength cannot be greater than MaxDocumentLength");
        }
        
        // Check for warnings
        string? warningMessage = null;
        if (parameters.MinSimilarityThreshold > 0.8f)
        {
            warningMessage = "Very high similarity threshold may result in few or no results";
        }
        else if (parameters.MinSimilarityThreshold < 0.1f)
        {
            warningMessage = "Very low similarity threshold may result in irrelevant results";
        }
        
        var hasErrors = issues.Count > 0;
        
        return new RetrievalParametersValidationResult
        {
            IsValid = !hasErrors,
            ErrorMessage = hasErrors ? string.Join("; ", issues) : null,
            WarningMessage = warningMessage,
            Issues = issues.Count > 0 ? issues.ToArray() : null
        };
    }
    
    private static IEnumerable<KnowledgeBaseResult> ApplyContentFilters(
        KnowledgeBaseResult[] results, 
        RetrievalParameters parameters)
    {
        var filtered = results.AsEnumerable();
        
        // Filter by content type if specified
        if (!string.IsNullOrEmpty(parameters.ContentTypeFilter))
        {
            filtered = filtered.Where(r => 
                r.Metadata?.ContainsKey("ContentType") == true &&
                r.Metadata["ContentType"]?.ToString()?.Equals(parameters.ContentTypeFilter, StringComparison.OrdinalIgnoreCase) == true);
        }
        
        // Filter by tags if specified
        if (parameters.TagsFilter?.Length > 0)
        {
            filtered = filtered.Where(r =>
            {
                if (r.Metadata?.ContainsKey("Tags") != true) return false;
                
                var tags = r.Metadata["Tags"] as string[] ?? 
                          r.Metadata["Tags"]?.ToString()?.Split(',', StringSplitOptions.RemoveEmptyEntries);
                
                if (tags == null) return false;
                
                return parameters.TagsFilter.Any(filterTag => 
                    tags.Any(tag => tag.Trim().Equals(filterTag, StringComparison.OrdinalIgnoreCase)));
            });
        }
        
        // Filter by document length if specified
        if (parameters.MinDocumentLength.HasValue)
        {
            filtered = filtered.Where(r => r.Content.Length >= parameters.MinDocumentLength.Value);
        }
        
        if (parameters.MaxDocumentLength.HasValue)
        {
            filtered = filtered.Where(r => r.Content.Length <= parameters.MaxDocumentLength.Value);
        }
        
        return filtered;
    }
    
    private static RetrievedDocument[] ConvertToRetrievedDocuments(
        IEnumerable<KnowledgeBaseResult> results, 
        RetrievalParameters parameters)
    {
        return results
            .Select((result, index) => new RetrievedDocument
            {
                Document = result,
                SimilarityScore = result.SimilarityScore,
                Rank = index + 1,
                RelevanceReason = parameters.IncludeRelevanceReasoning 
                    ? GenerateRelevanceReason(result)
                    : null
            })
            .ToArray();
    }
    
    private static string GenerateRelevanceReason(KnowledgeBaseResult result)
    {
        // Simple relevance reasoning - in a real application this could be more sophisticated
        var score = result.SimilarityScore;
        return score switch
        {
            >= 0.9f => "Very high semantic similarity with the query",
            >= 0.7f => "High semantic similarity with the query",
            >= 0.5f => "Moderate semantic similarity with the query",
            >= 0.3f => "Some semantic similarity with the query",
            _ => "Low but potential semantic relevance to the query"
        };
    }
    
    private static RetrievalMetadata CreateRetrievalMetadata(
        int totalFound,
        int returned,
        long timeMs,
        RetrievalParameters parameters,
        bool filteredBySimilarity,
        bool filteredByContent,
        RetrievedDocument[] finalResults)
    {
        var scores = finalResults.Select(d => d.SimilarityScore).ToArray();
        
        return new RetrievalMetadata
        {
            TotalDocumentsFound = totalFound,
            DocumentsReturned = returned,
            RetrievalTimeMs = timeMs,
            Parameters = parameters,
            DocumentsFilteredBySimilarity = filteredBySimilarity,
            DocumentsFilteredByContent = filteredByContent,
            AverageSimilarityScore = scores.Length > 0 ? scores.Average() : null,
            HighestSimilarityScore = scores.Length > 0 ? scores.Max() : null,
            LowestSimilarityScore = scores.Length > 0 ? scores.Min() : null
        };
    }
    
    private static ContextRetrievalResult CreateFailedResult(
        ProcessedQuery query, 
        RetrievalParameters parameters, 
        string errorMessage, 
        long timeMs)
    {
        var metadata = new RetrievalMetadata
        {
            TotalDocumentsFound = 0,
            DocumentsReturned = 0,
            RetrievalTimeMs = timeMs,
            Parameters = parameters,
            DocumentsFilteredBySimilarity = false,
            DocumentsFilteredByContent = false,
            AverageSimilarityScore = null,
            HighestSimilarityScore = null,
            LowestSimilarityScore = null
        };
        
        return new ContextRetrievalResult
        {
            Query = query,
            Documents = Array.Empty<RetrievedDocument>(),
            IsSuccessful = false,
            ErrorMessage = errorMessage,
            Metadata = metadata
        };
    }
}
