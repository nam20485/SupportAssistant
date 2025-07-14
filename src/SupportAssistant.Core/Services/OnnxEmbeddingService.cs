using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// ONNX-based embedding service that uses Microsoft Phi-3-mini or compatible models
/// for generating semantic embeddings.
/// </summary>
public class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly IOnnxRuntimeService _onnxRuntimeService;
    private InferenceSession? _session;
    private SessionOptions? _sessionOptions;
    private bool _isInitialized;
    private bool _disposed;
    private int _embeddingDimension = 384; // Default, will be updated based on model
    private readonly Dictionary<string, long[]> _inputShapes = new();
    private readonly Dictionary<string, string> _inputNames = new();

    public OnnxEmbeddingService(IOnnxRuntimeService onnxRuntimeService)
    {
        _onnxRuntimeService = onnxRuntimeService ?? throw new ArgumentNullException(nameof(onnxRuntimeService));
    }

    public async Task<bool> InitializeAsync(string modelPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException("Model path cannot be null or empty", nameof(modelPath));
            }

            if (!File.Exists(modelPath))
            {
                // For development, if model doesn't exist, we'll use a fallback mode
                // In production, this should be an error
                Console.WriteLine($"ONNX model not found at {modelPath}. Using fallback embedding generation.");
                _isInitialized = true;
                return await Task.FromResult(true);
            }

            // Initialize ONNX Runtime
            _onnxRuntimeService.Initialize();
            
            // Create session options with DirectML if available
            _sessionOptions = _onnxRuntimeService.CreateSessionOptions();
            
            // Load the ONNX model
            _session = new InferenceSession(modelPath, _sessionOptions);
            
            // Inspect model metadata
            await InspectModelMetadataAsync();
            
            _isInitialized = true;
            
            Console.WriteLine($"ONNX embedding model loaded successfully from {modelPath}");
            Console.WriteLine($"DirectML available: {_onnxRuntimeService.IsDirectMLAvailable()}");
            Console.WriteLine($"Available providers: {string.Join(", ", _onnxRuntimeService.GetAvailableProviders())}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize ONNX embedding service: {ex.Message}");
            
            // Fall back to non-ONNX mode for development
            _isInitialized = true;
            return false;
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service not initialized");

        if (string.IsNullOrWhiteSpace(text))
            return new float[_embeddingDimension];

        // If no ONNX session available, use fallback
        if (_session == null)
        {
            return await GenerateFallbackEmbeddingAsync(text);
        }

        try
        {
            return await GenerateOnnxEmbeddingAsync(text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ONNX embedding generation failed, using fallback: {ex.Message}");
            return await GenerateFallbackEmbeddingAsync(text);
        }
    }

    public async Task<IEnumerable<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service not initialized");

        var embeddings = new List<float[]>();
        
        // For now, process individually. In the future, we could implement true batch processing
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

    private async Task InspectModelMetadataAsync()
    {
        if (_session == null) return;

        try
        {
            // Examine input metadata
            var inputMetadata = _session.InputMetadata;
            foreach (var input in inputMetadata)
            {
                var inputName = input.Key;
                var metadata = input.Value;
                
                Console.WriteLine($"Input: {inputName}, Type: {metadata.ElementType}, Shape: [{string.Join(", ", metadata.Dimensions)}]");
                
                _inputNames[inputName] = inputName;
                _inputShapes[inputName] = metadata.Dimensions.Select(d => (long)d).ToArray();
            }

            // Examine output metadata to determine embedding dimension
            var outputMetadata = _session.OutputMetadata;
            foreach (var output in outputMetadata)
            {
                var outputName = output.Key;
                var metadata = output.Value;
                
                Console.WriteLine($"Output: {outputName}, Type: {metadata.ElementType}, Shape: [{string.Join(", ", metadata.Dimensions)}]");
                
                // Try to infer embedding dimension from output shape
                if (metadata.Dimensions.Length > 0)
                {
                    var lastDim = metadata.Dimensions[metadata.Dimensions.Length - 1];
                    if (lastDim > 0)
                    {
                        _embeddingDimension = (int)lastDim;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to inspect model metadata: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task<float[]> GenerateOnnxEmbeddingAsync(string text)
    {
        if (_session == null) 
            throw new InvalidOperationException("ONNX session not available");

        // This is a placeholder implementation
        // In a real implementation, we would:
        // 1. Tokenize the text using the appropriate tokenizer for Phi-3
        // 2. Create input tensors with the tokenized input
        // 3. Run inference to get embeddings
        // 4. Extract the appropriate embedding representation
        
        // For now, return a simple fallback
        Console.WriteLine("ONNX embedding generation not yet fully implemented, using fallback");
        return await GenerateFallbackEmbeddingAsync(text);
    }

    private async Task<float[]> GenerateFallbackEmbeddingAsync(string text)
    {
        // Use a deterministic approach that creates reasonable similarities
        // This is similar to SimpleEmbeddingService but with improvements
        
        var embedding = new float[_embeddingDimension];
        var normalizedText = text.ToLowerInvariant().Trim();
        
        // Use multiple hash seeds to create different features
        var words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Create embedding based on word hashes and text features
        for (var i = 0; i < _embeddingDimension; i++)
        {
            var featureValue = 0f;
            
            // Word-based features
            foreach (var word in words)
            {
                var wordHash = word.GetHashCode();
                var wordSeed = wordHash ^ (i * 17); // Different feature for each dimension
                var wordRandom = new Random(wordSeed);
                featureValue += (float)(wordRandom.NextDouble() * 2.0 - 1.0) / words.Length;
            }
            
            // Text-level features
            var textHash = normalizedText.GetHashCode();
            var textSeed = textHash ^ (i * 31);
            var textRandom = new Random(textSeed);
            var textFeature = (float)(textRandom.NextDouble() * 2.0 - 1.0) * 0.3f;
            
            embedding[i] = featureValue + textFeature;
        }
        
        // Add some structured features for better similarity
        if (_embeddingDimension > 10)
        {
            embedding[0] += Math.Min(normalizedText.Length / 1000f, 1f) * 0.5f;
            embedding[1] += Math.Min(words.Length / 100f, 1f) * 0.5f;
            embedding[2] += Math.Min(normalizedText.Distinct().Count() / 50f, 1f) * 0.5f;
        }
        
        // Normalize to unit vector
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }
        }
        
        return await Task.FromResult(embedding);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _session?.Dispose();
                _sessionOptions?.Dispose();
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
