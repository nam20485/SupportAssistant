using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Implementation of the knowledge base service that orchestrates document ingestion,
/// text processing, embedding generation, and similarity search.
/// </summary>
public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStorageService _vectorStorageService;
    
    private bool _isInitialized;
    private string _storagePath = string.Empty;

    public KnowledgeBaseService(
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IVectorStorageService vectorStorageService)
    {
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStorageService = vectorStorageService ?? throw new ArgumentNullException(nameof(vectorStorageService));
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            // Set up storage path
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var knowledgeBasePath = Path.Combine(appDataPath, "SupportAssistant", "KnowledgeBase");
            
            if (!Directory.Exists(knowledgeBasePath))
            {
                Directory.CreateDirectory(knowledgeBasePath);
            }

            _storagePath = Path.Combine(knowledgeBasePath, "vectors.json");

            // Initialize services
            var embeddingInitialized = await _embeddingService.InitializeAsync(string.Empty);
            if (!embeddingInitialized)
                return false;

            var embeddingDimension = _embeddingService.GetEmbeddingDimension();
            var storageInitialized = await _vectorStorageService.InitializeAsync(_storagePath, embeddingDimension);
            if (!storageInitialized)
                return false;

            _isInitialized = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IngestDocumentAsync(string content, string source, Dictionary<string, object>? metadata = null)
    {
        if (!_isInitialized)
            return false;

        if (string.IsNullOrWhiteSpace(content))
            return false;

        try
        {
            // Chunk the document intelligently
            var chunks = _chunkingService.ChunkTextIntelligent(content, maxChunkSize: 1000, overlapSize: 200);

            var tasks = new List<Task<bool>>();

            foreach (var chunk in chunks)
            {
                tasks.Add(ProcessChunkAsync(chunk, source, metadata));
            }

            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<KnowledgeBaseResult>> SearchAsync(string query, int maxResults = 5, float similarityThreshold = 0.7f)
    {
        if (!_isInitialized || string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<KnowledgeBaseResult>();

        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            // Search the vector storage
            var searchResults = await _vectorStorageService.SearchAsync(queryEmbedding, maxResults, similarityThreshold);

            // Convert to knowledge base results
            var results = searchResults.Select(sr => new KnowledgeBaseResult
            {
                Content = sr.Content,
                Source = sr.Source,
                SimilarityScore = sr.SimilarityScore,
                Metadata = sr.Metadata,
                ChunkIndex = sr.Metadata?.ContainsKey("chunkIndex") == true ? 
                    Convert.ToInt32(sr.Metadata["chunkIndex"]) : 0
            });

            return results;
        }
        catch
        {
            return Enumerable.Empty<KnowledgeBaseResult>();
        }
    }

    public async Task<KnowledgeBaseStatistics> GetStatisticsAsync()
    {
        try
        {
            var chunkCount = await _vectorStorageService.GetChunkCountAsync();
            var storageInfo = new FileInfo(_storagePath);
            
            return new KnowledgeBaseStatistics
            {
                DocumentCount = await EstimateDocumentCountAsync(),
                ChunkCount = chunkCount,
                TotalCharacters = await EstimateTotalCharactersAsync(),
                LastUpdated = storageInfo.Exists ? storageInfo.LastWriteTime : DateTime.MinValue,
                StorageLocation = _storagePath
            };
        }
        catch
        {
            return new KnowledgeBaseStatistics
            {
                DocumentCount = 0,
                ChunkCount = 0,
                TotalCharacters = 0,
                LastUpdated = DateTime.MinValue,
                StorageLocation = _storagePath
            };
        }
    }

    private async Task<bool> ProcessChunkAsync(TextChunk chunk, string source, Dictionary<string, object>? metadata)
    {
        try
        {
            // Generate embedding for the chunk
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);

            // Create chunk metadata
            var chunkMetadata = new Dictionary<string, object>(metadata ?? new Dictionary<string, object>())
            {
                ["chunkIndex"] = chunk.ChunkIndex,
                ["startIndex"] = chunk.StartIndex,
                ["endIndex"] = chunk.EndIndex,
                ["length"] = chunk.Length
            };

            // Generate unique ID for the chunk
            var chunkId = $"{source}#{chunk.ChunkIndex}";

            // Store the chunk with its embedding
            return await _vectorStorageService.StoreAsync(chunkId, chunk.Content, embedding, source, chunkMetadata);
        }
        catch
        {
            return false;
        }
    }

    private async Task<int> EstimateDocumentCountAsync()
    {
        // This is a simple estimation based on unique sources
        // In a real implementation, we'd track this more precisely
        try
        {
            var searchResults = await _vectorStorageService.SearchAsync(
                new float[_embeddingService.GetEmbeddingDimension()], 
                maxResults: 1000, 
                similarityThreshold: 0.0f);
            
            var uniqueSources = searchResults.Select(r => r.Source).Distinct().Count();
            return uniqueSources;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<long> EstimateTotalCharactersAsync()
    {
        // Estimate total characters from stored chunks
        try
        {
            var searchResults = await _vectorStorageService.SearchAsync(
                new float[_embeddingService.GetEmbeddingDimension()], 
                maxResults: 1000, 
                similarityThreshold: 0.0f);
            
            return searchResults.Sum(r => r.Content.Length);
        }
        catch
        {
            return 0;
        }
    }
}
