using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Simple embedding service implementation that generates deterministic embeddings
/// based on text content. This is a placeholder implementation that can be replaced
/// with actual ONNX model-based embeddings.
/// </summary>
public class SimpleEmbeddingService : IEmbeddingService
{
    private bool _isInitialized;
    private readonly int _embeddingDimension;

    public SimpleEmbeddingService(int embeddingDimension = 384)
    {
        _embeddingDimension = embeddingDimension;
    }

    public async Task<bool> InitializeAsync(string modelPath)
    {
        // For this simple implementation, we don't need to load a model
        // In a real implementation, this would load the ONNX model
        _isInitialized = true;
        return await Task.FromResult(true);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service not initialized");

        if (string.IsNullOrWhiteSpace(text))
            return new float[_embeddingDimension];

        return await Task.FromResult(GenerateDeterministicEmbedding(text));
    }

    public async Task<IEnumerable<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service not initialized");

        var embeddings = new List<float[]>();
        
        foreach (var text in texts)
        {
            embeddings.Add(await GenerateEmbeddingAsync(text));
        }

        return embeddings;
    }

    public int GetEmbeddingDimension()
    {
        return _embeddingDimension;
    }

    public float CalculateCosineSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("Embeddings must have the same dimension");

        var dotProduct = 0f;
        var magnitude1 = 0f;
        var magnitude2 = 0f;

        for (var i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            magnitude1 += embedding1[i] * embedding1[i];
            magnitude2 += embedding2[i] * embedding2[i];
        }

        magnitude1 = (float)Math.Sqrt(magnitude1);
        magnitude2 = (float)Math.Sqrt(magnitude2);

        if (magnitude1 == 0f || magnitude2 == 0f)
            return 0f;

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Generates a deterministic embedding based on text content.
    /// This is a simple hash-based approach for demonstration.
    /// In production, this would be replaced with actual neural network embeddings.
    /// </summary>
    private float[] GenerateDeterministicEmbedding(string text)
    {
        var embedding = new float[_embeddingDimension];
        
        // Normalize text
        var normalizedText = text.ToLowerInvariant().Trim();
        
        // Use SHA256 hash as basis for embedding
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedText));
        
        // Create multiple hash variants to fill the embedding dimension
        var hashVariants = new List<byte[]> { hash };
        
        // Generate additional hash variants by appending indices
        var requiredBytes = _embeddingDimension * sizeof(float);
        while (hashVariants.Sum(h => h.Length) < requiredBytes)
        {
            var variant = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{normalizedText}_{hashVariants.Count}"));
            hashVariants.Add(variant);
        }
        
        // Convert hash bytes to float values
        var allBytes = hashVariants.SelectMany(h => h).ToArray();
        for (var i = 0; i < _embeddingDimension; i++)
        {
            var byteIndex = (i * 4) % allBytes.Length;
            
            // Convert 4 bytes to float, normalize to [-1, 1] range
            var intValue = BitConverter.ToInt32(allBytes, byteIndex);
            embedding[i] = intValue / (float)int.MaxValue;
        }
        
        // Add some text-based features for better semantic similarity
        AddTextFeatures(embedding, normalizedText);
        
        // Normalize the embedding vector
        NormalizeVector(embedding);
        
        return embedding;
    }

    /// <summary>
    /// Adds text-based features to improve semantic similarity.
    /// </summary>
    private static void AddTextFeatures(float[] embedding, string text)
    {
        // Add length-based feature
        var lengthFeature = Math.Min(text.Length / 1000f, 1f);
        
        // Add word count feature
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var wordCountFeature = Math.Min(wordCount / 100f, 1f);
        
        // Add character diversity feature
        var uniqueChars = text.Distinct().Count();
        var diversityFeature = Math.Min(uniqueChars / 50f, 1f);
        
        // Blend these features into the embedding
        var featureWeight = 0.1f;
        if (embedding.Length > 3)
        {
            embedding[0] = embedding[0] * (1 - featureWeight) + lengthFeature * featureWeight;
            embedding[1] = embedding[1] * (1 - featureWeight) + wordCountFeature * featureWeight;
            embedding[2] = embedding[2] * (1 - featureWeight) + diversityFeature * featureWeight;
        }
    }

    /// <summary>
    /// Normalizes the embedding vector to unit length.
    /// </summary>
    private static void NormalizeVector(float[] vector)
    {
        var magnitude = (float)Math.Sqrt(vector.Sum(x => x * x));
        
        if (magnitude > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }
    }
}
