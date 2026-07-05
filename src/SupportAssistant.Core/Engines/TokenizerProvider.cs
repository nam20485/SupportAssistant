using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.Tokenizers;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Core.Engines
{
    /// <summary>
    /// Loads (and lazily fetches from Hugging Face on first use) the tokenizer vocab files the
    /// engines need. InferenceEngine.Core fetches the ONNX <em>models</em> via
    /// <c>ModelDownloadUrls</c>, but it is pure tensor-in/out, so tokenization — and its assets —
    /// remain ours. Files are cached on disk under the configured tokenizer paths.
    /// </summary>
    public static class TokenizerProvider
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

        private const string MinilmVocabUrl =
            "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/vocab.txt";

        private const string Phi3TokenizerModelUrl =
            "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx/resolve/main/cpu_and_mobile/cpu-int4-rtn-block-32/tokenizer.model";

        /// <summary>
        /// Returns a BERT WordPiece tokenizer for all-MiniLM-L6-v2 (uncased). Fetches
        /// <c>vocab.txt</c> on first use. Returns null if the vocab cannot be obtained.
        /// </summary>
        public static async Task<Tokenizer?> GetEmbeddingTokenizerAsync(
            IConfigurationService config, CancellationToken ct = default)
        {
            var path = await EnsureFileAsync(config.GetEmbeddingTokenizerPath(), MinilmVocabUrl, ct)
                .ConfigureAwait(false);
            if (path == null)
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            // MiniLM is an uncased model, so lowercase before tokenization.
            var tokenizer = BertTokenizer.Create(stream, new BertOptions
            {
                LowerCaseBeforeTokenization = true
            });
            return tokenizer;
        }

        /// <summary>
        /// Returns a SentencePiece tokenizer for Phi-3-mini-4k-instruct. Fetches
        /// <c>tokenizer.model</c> on first use. Returns null if the model cannot be obtained.
        /// </summary>
        public static async Task<Tokenizer?> GetGenerationTokenizerAsync(
            IConfigurationService config, CancellationToken ct = default)
        {
            var path = await EnsureFileAsync(config.GetGenerationTokenizerModelPath(), Phi3TokenizerModelUrl, ct)
                .ConfigureAwait(false);
            if (path == null)
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            // Phi-3 wraps prompts in its own chat markers; the raw SentencePiece model should not
            // auto-add <s>/</s> around the already-framed token sequence. LlamaTokenizer is the
            // SentencePiece-backed factory exposed by Microsoft.ML.Tokenizers 1.0.3.
            var tokenizer = LlamaTokenizer.Create(stream, addBeginOfSentence: false,
                addEndOfSentence: false, specialTokens: SpecialTokens);
            return tokenizer;
        }

        /// <summary>
        /// Phi-3 added/special tokens (id map) used by the SentencePiece tokenizer. The base
        /// SentencePiece model covers ids 0..~32k; these extended control tokens stop generation.
        /// </summary>
        internal static readonly System.Collections.Generic.IReadOnlyDictionary<string, int> SpecialTokens =
            new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["<|endoftext|>"] = 32000,
                ["<|im_start|>"] = 32001,
                ["<|im_end|>"] = 32002
            };

        /// <summary>Ensures <paramref name="targetPath"/> exists, downloading from
        /// <paramref name="url"/> if missing. Returns the path, or null on failure.</summary>
        private static async Task<string?> EnsureFileAsync(string targetPath, string url, CancellationToken ct)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    return targetPath;
                }

                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var outStream = File.Create(targetPath);
                await response.Content.CopyToAsync(outStream, ct).ConfigureAwait(false);

                return targetPath;
            }
            catch
            {
                return null;
            }
        }
    }
}
