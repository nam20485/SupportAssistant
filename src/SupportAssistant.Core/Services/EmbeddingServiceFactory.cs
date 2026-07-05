using System;
using System.Threading.Tasks;
using InferenceEngine.Core.Text;
using Microsoft.Extensions.Logging;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Factory for creating the embedding service. Wraps the library's <see cref="TextEmbeddingEngine"/>
/// in an <see cref="OnnxEmbeddingService"/> adapter (real embeddings; the engine owns model/tokenizer
/// fetching and loads lazily). Falls back to <see cref="SimpleEmbeddingService"/> only on a hard
/// construction failure — per-prediction unavailability is handled inside the adapter.
/// </summary>
public interface IEmbeddingServiceFactory
{
    /// <summary>
    /// Creates and initializes the appropriate embedding service
    /// </summary>
    Task<IEmbeddingService> CreateEmbeddingServiceAsync();
}

/// <summary>
/// Default embedding service factory implementation
/// </summary>
public class EmbeddingServiceFactory : IEmbeddingServiceFactory
{
    private readonly TextEmbeddingEngine _engine;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<EmbeddingServiceFactory>? _logger;

    public EmbeddingServiceFactory(
        TextEmbeddingEngine engine,
        IConfigurationService configurationService,
        ILogger<EmbeddingServiceFactory>? logger = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger;
    }

    public async Task<IEmbeddingService> CreateEmbeddingServiceAsync()
    {
        try
        {
            // The engine fetches its model + tokenizer lazily on first prediction; no eager load here.
            var service = new OnnxEmbeddingService(_engine);
            await service.InitializeAsync(_configurationService.GetEmbeddingModelPath()).ConfigureAwait(false);
            return service;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ONNX embedding service construction failed; using simple embeddings.");
            return new SimpleEmbeddingService(_configurationService.GetEmbeddingDimension());
        }
    }
}
