using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InferenceEngine.Core.Text;
using Microsoft.Extensions.Logging;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Embedding service that delegates to the library's <see cref="TextEmbeddingEngine"/>
/// (all-MiniLM-L6-v2: real tokenization + ONNX inference + pooling/normalization, with the engine
/// owning model/tokenizer fetching). Falls back to a deterministic hash embedding only when the
/// engine is unavailable (no model/network) or a prediction fails.
/// </summary>
public class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly TextEmbeddingEngine _engine;
    private readonly ILogger<OnnxEmbeddingService>? _logger;
    private bool _disposed;
    private int _embeddingDimension = 384;

    public OnnxEmbeddingService(TextEmbeddingEngine engine, ILogger<OnnxEmbeddingService>? logger = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger;
    }

    /// <summary>
    /// Retained for interface compatibility. The engine loads lazily on first prediction (and
    /// fetches its model/tokenizer on demand), so there is nothing to pre-initialize here.
    /// </summary>
    public Task<bool> InitializeAsync(string modelPath) => Task.FromResult(true);

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[_embeddingDimension];
        }

        try
        {
            var embedding = await _engine.PredictAsync(text).ConfigureAwait(false);
            if (embedding is { Length: > 0 })
            {
                _embeddingDimension = embedding.Length;
                return embedding;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ONNX embedding prediction failed; using hash fallback.");
        }

        return await GenerateFallbackEmbeddingAsync(text).ConfigureAwait(false);
    }

    public async Task<IEnumerable<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts)
    {
        var embeddings = new List<float[]>();
        foreach (var text in texts)
        {
            embeddings.Add(await GenerateEmbeddingAsync(text).ConfigureAwait(false));
        }

        return embeddings;
    }

    public int GetEmbeddingDimension() => _embeddingDimension;

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
    /// Deterministic hash-based embedding used when the real model is unavailable. Mirrors the
    /// legacy fallback so retrieval still works in degraded/development environments.
    /// </summary>
    private async Task<float[]> GenerateFallbackEmbeddingAsync(string text)
    {
        var embedding = new float[_embeddingDimension];
        var normalizedText = text.ToLowerInvariant().Trim();
        var words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < _embeddingDimension; i++)
        {
            var featureValue = 0f;
            foreach (var word in words)
            {
                var wordSeed = word.GetHashCode() ^ (i * 17);
                var wordRandom = new Random(wordSeed);
                featureValue += (float)(wordRandom.NextDouble() * 2.0 - 1.0) / Math.Max(1, words.Length);
            }

            var textSeed = normalizedText.GetHashCode() ^ (i * 31);
            var textRandom = new Random(textSeed);
            featureValue += (float)(textRandom.NextDouble() * 2.0 - 1.0) * 0.3f;
            embedding[i] = featureValue;
        }

        if (_embeddingDimension > 10)
        {
            embedding[0] += Math.Min(normalizedText.Length / 1000f, 1f) * 0.5f;
            embedding[1] += Math.Min(words.Length / 100f, 1f) * 0.5f;
            embedding[2] += Math.Min(normalizedText.Distinct().Count() / 50f, 1f) * 0.5f;
        }

        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return await Task.FromResult(embedding).ConfigureAwait(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                (_engine as IDisposable)?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
