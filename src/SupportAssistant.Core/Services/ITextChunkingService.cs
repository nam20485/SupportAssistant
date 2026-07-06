using System;
using System.Collections.Generic;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Service for splitting text into chunks suitable for embedding and retrieval.
/// </summary>
public interface ITextChunkingService
{
    /// <summary>
    /// Splits text into chunks with optional overlap for better context preservation.
    /// </summary>
    /// <param name="text">The text to chunk.</param>
    /// <param name="maxChunkSize">Maximum characters per chunk (default: 1000).</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks (default: 200).</param>
    /// <returns>A list of text chunks.</returns>
    IEnumerable<TextChunk> ChunkText(string text, int maxChunkSize = 1000, int overlapSize = 200);

    /// <summary>
    /// Splits text intelligently by trying to break at sentence boundaries, paragraphs, etc.
    /// </summary>
    /// <param name="text">The text to chunk.</param>
    /// <param name="maxChunkSize">Maximum characters per chunk (default: 1000).</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks (default: 200).</param>
    /// <returns>A list of intelligently chunked text pieces.</returns>
    IEnumerable<TextChunk> ChunkTextIntelligent(string text, int maxChunkSize = 1000, int overlapSize = 200);
}

/// <summary>
/// Represents a chunk of text with its position and metadata.
/// </summary>
public class TextChunk
{
    public required string Content { get; init; }
    public int StartIndex { get; init; }
    public int EndIndex { get; init; }
    public int ChunkIndex { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    public int Length => Content.Length;
}
