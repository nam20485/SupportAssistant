using System;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Factory for creating embedding services based on configuration
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
    private readonly IOnnxRuntimeService _onnxRuntimeService;
    private readonly IConfigurationService _configurationService;

    public EmbeddingServiceFactory(
        IOnnxRuntimeService onnxRuntimeService,
        IConfigurationService configurationService)
    {
        _onnxRuntimeService = onnxRuntimeService ?? throw new ArgumentNullException(nameof(onnxRuntimeService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    public async Task<IEmbeddingService> CreateEmbeddingServiceAsync()
    {
        IEmbeddingService embeddingService;
        
        if (_configurationService.UseOnnxEmbeddings())
        {
            // Try ONNX embedding service first
            var onnxService = new OnnxEmbeddingService(_onnxRuntimeService);
            var modelPath = _configurationService.GetModelPath();
            
            try
            {
                var initialized = await onnxService.InitializeAsync(modelPath);
                if (initialized)
                {
                    Console.WriteLine($"Using ONNX embedding service with model: {modelPath}");
                    embeddingService = onnxService;
                }
                else
                {
                    Console.WriteLine("ONNX embedding service initialization failed, falling back to simple embeddings");
                    onnxService.Dispose();
                    embeddingService = new SimpleEmbeddingService(_configurationService.GetEmbeddingDimension());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize ONNX embedding service: {ex.Message}");
                onnxService.Dispose();
                embeddingService = new SimpleEmbeddingService(_configurationService.GetEmbeddingDimension());
            }
        }
        else
        {
            Console.WriteLine("Using simple embedding service (configured)");
            embeddingService = new SimpleEmbeddingService(_configurationService.GetEmbeddingDimension());
        }

        // Initialize the selected service
        await embeddingService.InitializeAsync(_configurationService.GetModelPath());
        
        return embeddingService;
    }
}
