using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for generating embeddings from text using ONNX models.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Initializes the embedding service with the specified model.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX embedding model.</param>
    /// <returns>True if initialization was successful, false otherwise.</returns>
    Task<bool> InitializeAsync(string modelPath);

    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to generate an embedding for.</param>
    /// <returns>The embedding vector as a float array.</returns>
    Task<float[]> GenerateEmbeddingAsync(string text);

    /// <summary>
    /// Generates embeddings for multiple texts in a batch for better performance.
    /// </summary>
    /// <param name="texts">The texts to generate embeddings for.</param>
    /// <returns>A list of embedding vectors corresponding to the input texts.</returns>
    Task<IEnumerable<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts);

    /// <summary>
    /// Gets the dimension of the embedding vectors produced by this service.
    /// </summary>
    /// <returns>The embedding dimension (e.g., 384 for all-MiniLM-L6-v2).</returns>
    int GetEmbeddingDimension();

    /// <summary>
    /// Calculates the cosine similarity between two embedding vectors.
    /// </summary>
    /// <param name="embedding1">The first embedding vector.</param>
    /// <param name="embedding2">The second embedding vector.</param>
    /// <returns>The cosine similarity score (0.0 to 1.0).</returns>
    float CalculateCosineSimilarity(float[] embedding1, float[] embedding2);
}
