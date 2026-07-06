using System;
using System.IO;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Configuration service for SupportAssistant application settings.
/// </summary>
/// <remarks>
/// Phase 4 splits the single Phi-3 model into two ONNX models with independent paths:
/// an embedding model (all-MiniLM-L6-v2) and a generation model (Phi-3-mini-4k-instruct),
/// each with its own tokenizer vocab files. The legacy <see cref="GetModelPath"/> is retained
/// for backward compatibility and resolves to the generation model path.
/// </remarks>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the path to the ONNX model file (legacy single-model accessor; resolves to the
    /// generation model path).
    /// </summary>
    string GetModelPath();

    /// <summary>
    /// Gets the path to the embedding ONNX model (all-MiniLM-L6-v2).
    /// </summary>
    string GetEmbeddingModelPath();

    /// <summary>
    /// Gets the path to the generation ONNX model (Phi-3-mini-4k-instruct).
    /// </summary>
    string GetGenerationModelPath();

    /// <summary>
    /// Gets the path to the embedding tokenizer vocab file (MiniLM <c>vocab.txt</c>).
    /// </summary>
    string GetEmbeddingTokenizerPath();

    /// <summary>
    /// Gets the path to the generation SentencePiece tokenizer model file (Phi-3
    /// <c>tokenizer.model</c>). Phi-3 uses a SentencePiece tokenizer, not a BPE vocab/merges pair.
    /// </summary>
    string GetGenerationTokenizerModelPath();

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
    private readonly string _modelsPath;
    private readonly string _tokenizersPath;

    public DefaultConfigurationService()
    {
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SupportAssistant");
        _modelsPath = Path.Combine(_appDataPath, "Models");
        _tokenizersPath = Path.Combine(_appDataPath, "Tokenizers");
    }

    public string GetModelPath() => GetGenerationModelPath();

    public string GetEmbeddingModelPath() =>
        ResolveFirst(
            Path.Combine(_modelsPath, "all-MiniLM-L6-v2.onnx"),
            Path.Combine(_modelsPath, "minilm-l6-v2.onnx"),
            Path.Combine(Environment.CurrentDirectory, "Models", "all-MiniLM-L6-v2.onnx"));

    public string GetGenerationModelPath() =>
        ResolveFirst(
            Path.Combine(_modelsPath, "phi-3-mini-4k-instruct.onnx"),
            Path.Combine(_modelsPath, "phi-3-mini.onnx"),
            Path.Combine(Environment.CurrentDirectory, "Models", "phi-3-mini.onnx"));

    public string GetEmbeddingTokenizerPath() =>
        ResolveFirst(
            Path.Combine(_tokenizersPath, "minilm", "vocab.txt"),
            Path.Combine(_modelsPath, "minilm", "vocab.txt"));

    public string GetGenerationTokenizerModelPath() =>
        ResolveFirst(
            Path.Combine(_tokenizersPath, "phi3", "tokenizer.model"),
            Path.Combine(_modelsPath, "phi3", "tokenizer.model"));

    public bool UseOnnxEmbeddings()
    {
        // For now, always try ONNX first, with fallback to simple embeddings
        return true;
    }

    public int GetEmbeddingDimension()
    {
        // Default embedding dimension for all-MiniLM-L6-v2
        return 384;
    }

    public string GetKnowledgeBaseDirectory()
    {
        return Path.Combine(_appDataPath, "KnowledgeBase");
    }

    /// <summary>
    /// Returns the first candidate path that exists on disk, else the first candidate
    /// (callers handle the "model genuinely unavailable" case by checking existence).
    /// </summary>
    private static string ResolveFirst(params string[] candidates)
    {
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return candidates[0];
    }
}
