using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// File-based vector storage implementation that stores embeddings and metadata
/// in JSON format with efficient similarity search capabilities.
/// </summary>
public class FileVectorStorageService : IVectorStorageService
{
    private string _storagePath;
    private readonly List<StoredVector> _vectors;
    private int _embeddingDimension;
    private bool _isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileVectorStorageService()
    {
        _storagePath = string.Empty;
        _vectors = new List<StoredVector>();
    }

    public async Task<bool> InitializeAsync(string storagePath, int embeddingDimension)
    {
        try
        {
            _storagePath = storagePath;
            _embeddingDimension = embeddingDimension;
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(storagePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Load existing vectors if file exists
            if (File.Exists(storagePath))
            {
                await LoadVectorsFromFileAsync();
            }

            _isInitialized = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StoreAsync(string id, string content, float[] embedding, string source, Dictionary<string, object>? metadata = null)
    {
        if (!_isInitialized)
            return false;

        if (embedding.Length != _embeddingDimension)
            return false;

        try
        {
            var storedVector = new StoredVector
            {
                Id = id,
                Content = content,
                Embedding = embedding,
                Source = source,
                Metadata = metadata ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow
            };

            // Remove existing vector with same ID if it exists
            _vectors.RemoveAll(v => v.Id == id);
            
            // Add new vector
            _vectors.Add(storedVector);

            // Save to file
            await SaveVectorsToFileAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int maxResults = 5, float similarityThreshold = 0.7f)
    {
        if (!_isInitialized || queryEmbedding.Length != _embeddingDimension)
            return Enumerable.Empty<VectorSearchResult>();

        try
        {
            var results = _vectors
                .Select(v => new
                {
                    Vector = v,
                    Similarity = CalculateCosineSimilarity(queryEmbedding, v.Embedding)
                })
                .Where(r => r.Similarity >= similarityThreshold)
                .OrderByDescending(r => r.Similarity)
                .Take(maxResults)
                .Select(r => new VectorSearchResult
                {
                    Id = r.Vector.Id,
                    Content = r.Vector.Content,
                    Source = r.Vector.Source,
                    SimilarityScore = r.Similarity,
                    Metadata = r.Vector.Metadata
                });

            return await Task.FromResult(results);
        }
        catch
        {
            return Enumerable.Empty<VectorSearchResult>();
        }
    }

    public async Task<int> GetChunkCountAsync()
    {
        return await Task.FromResult(_vectors.Count);
    }

    public async Task<bool> ClearAsync()
    {
        try
        {
            _vectors.Clear();
            
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }

            return await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> OptimizeAsync()
    {
        // For file-based storage, optimization is just a clean save
        try
        {
            await SaveVectorsToFileAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadVectorsFromFileAsync()
    {
        if (!File.Exists(_storagePath))
            return;

        var json = await File.ReadAllTextAsync(_storagePath);
        var vectorData = JsonSerializer.Deserialize<VectorStorageData>(json, JsonOptions);
        
        if (vectorData != null)
        {
            _vectors.Clear();
            _vectors.AddRange(vectorData.Vectors);
            _embeddingDimension = vectorData.EmbeddingDimension;
        }
    }

    private async Task SaveVectorsToFileAsync()
    {
        var vectorData = new VectorStorageData
        {
            EmbeddingDimension = _embeddingDimension,
            Vectors = _vectors.ToList(),
            LastUpdated = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(vectorData, JsonOptions);
        await File.WriteAllTextAsync(_storagePath, json);
    }

    private static float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            return 0f;

        var dotProduct = 0f;
        var magnitudeA = 0f;
        var magnitudeB = 0f;

        for (var i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}

/// <summary>
/// Internal representation of a stored vector with metadata.
/// </summary>
internal class StoredVector
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required float[] Embedding { get; init; }
    public required string Source { get; init; }
    public required Dictionary<string, object> Metadata { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Data structure for JSON serialization of the vector storage.
/// </summary>
internal class VectorStorageData
{
    public int EmbeddingDimension { get; init; }
    public required List<StoredVector> Vectors { get; init; }
    public DateTime LastUpdated { get; init; }
}
