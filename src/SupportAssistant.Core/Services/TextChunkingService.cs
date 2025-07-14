using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Implementation of text chunking service that splits text into manageable pieces
/// while preserving context through overlapping chunks.
/// </summary>
public class TextChunkingService : ITextChunkingService
{
    private static readonly char[] SentenceEnders = ['.', '!', '?'];
    private static readonly char[] ParagraphBreaks = ['\n', '\r'];
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public IEnumerable<TextChunk> ChunkText(string text, int maxChunkSize = 1000, int overlapSize = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var cleanText = WhitespaceRegex.Replace(text.Trim(), " ");
        
        if (cleanText.Length <= maxChunkSize)
        {
            yield return new TextChunk
            {
                Content = cleanText,
                StartIndex = 0,
                EndIndex = cleanText.Length - 1,
                ChunkIndex = 0
            };
            yield break;
        }

        var currentIndex = 0;
        var chunkIndex = 0;

        while (currentIndex < cleanText.Length)
        {
            var endIndex = Math.Min(currentIndex + maxChunkSize, cleanText.Length);
            var chunkContent = cleanText.Substring(currentIndex, endIndex - currentIndex);

            yield return new TextChunk
            {
                Content = chunkContent,
                StartIndex = currentIndex,
                EndIndex = endIndex - 1,
                ChunkIndex = chunkIndex
            };

            currentIndex = Math.Max(endIndex - overlapSize, currentIndex + 1);
            chunkIndex++;

            // Prevent infinite loop if we've reached the end
            if (currentIndex >= cleanText.Length) break;
        }
    }

    public IEnumerable<TextChunk> ChunkTextIntelligent(string text, int maxChunkSize = 1000, int overlapSize = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var cleanText = WhitespaceRegex.Replace(text.Trim(), " ");
        
        if (cleanText.Length <= maxChunkSize)
        {
            yield return new TextChunk
            {
                Content = cleanText,
                StartIndex = 0,
                EndIndex = cleanText.Length - 1,
                ChunkIndex = 0
            };
            yield break;
        }

        var currentIndex = 0;
        var chunkIndex = 0;

        while (currentIndex < cleanText.Length)
        {
            var endIndex = Math.Min(currentIndex + maxChunkSize, cleanText.Length);
            
            // Try to find a good breaking point
            if (endIndex < cleanText.Length)
            {
                var breakPoint = FindBestBreakPoint(cleanText, currentIndex, endIndex);
                if (breakPoint > currentIndex)
                {
                    endIndex = breakPoint;
                }
            }

            var chunkContent = cleanText.Substring(currentIndex, endIndex - currentIndex).Trim();

            if (!string.IsNullOrEmpty(chunkContent))
            {
                yield return new TextChunk
                {
                    Content = chunkContent,
                    StartIndex = currentIndex,
                    EndIndex = endIndex - 1,
                    ChunkIndex = chunkIndex
                };
                chunkIndex++;
            }

            // Calculate next starting position with overlap
            var nextStart = endIndex - overlapSize;
            if (nextStart <= currentIndex)
            {
                nextStart = currentIndex + 1; // Ensure forward progress
            }

            // Prevent infinite loop - must make forward progress
            if (nextStart >= cleanText.Length) break;
            
            currentIndex = nextStart;
        }
    }

    private static int FindBestBreakPoint(string text, int startIndex, int maxEndIndex)
    {
        var searchStart = Math.Max(startIndex, maxEndIndex - 200); // Look in last 200 chars for break point
        
        // Look for paragraph break first
        for (var i = maxEndIndex - 1; i >= searchStart; i--)
        {
            if (ParagraphBreaks.Contains(text[i]))
            {
                return i + 1;
            }
        }

        // Look for sentence end
        for (var i = maxEndIndex - 1; i >= searchStart; i--)
        {
            if (SentenceEnders.Contains(text[i]))
            {
                // Make sure it's not an abbreviation (simple check)
                if (i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                {
                    return i + 1;
                }
            }
        }

        // Look for word boundary (space)
        for (var i = maxEndIndex - 1; i >= searchStart; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        // No good break point found, use original position
        return maxEndIndex;
    }
}
