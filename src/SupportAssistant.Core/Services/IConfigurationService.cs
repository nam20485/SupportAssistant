using System;
using System.IO;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Configuration service for SupportAssistant application settings
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the path to the ONNX model file
    /// </summary>
    string GetModelPath();
    
    /// <summary>
    /// Gets whether to use ONNX embeddings or fallback to simple embeddings
    /// </summary>
    bool UseOnnxEmbeddings();
    
    /// <summary>
    /// Gets the embedding dimension to use
    /// </summary>
    int GetEmbeddingDimension();
    
    /// <summary>
    /// Gets the base directory for knowledge base storage
    /// </summary>
    string GetKnowledgeBaseDirectory();
}

/// <summary>
/// Default configuration service implementation
/// </summary>
public class DefaultConfigurationService : IConfigurationService
{
    private readonly string _appDataPath;
    
    public DefaultConfigurationService()
    {
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SupportAssistant");
    }

    public string GetModelPath()
    {
        // Look for Phi-3-mini model in several locations
        var possiblePaths = new[]
        {
            Path.Combine(_appDataPath, "Models", "phi-3-mini.onnx"),
            Path.Combine(_appDataPath, "Models", "phi-3-mini-4k-instruct.onnx"),
            Path.Combine(Environment.CurrentDirectory, "Models", "phi-3-mini.onnx"),
            Path.Combine(Environment.CurrentDirectory, "phi-3-mini.onnx")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Return the preferred path even if it doesn't exist
        // The service will handle fallback mode
        return possiblePaths[0];
    }

    public bool UseOnnxEmbeddings()
    {
        // For now, always try ONNX first, with fallback to simple embeddings
        return true;
    }

    public int GetEmbeddingDimension()
    {
        // Default embedding dimension
        return 384;
    }

    public string GetKnowledgeBaseDirectory()
    {
        return Path.Combine(_appDataPath, "KnowledgeBase");
    }
}
