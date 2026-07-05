using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InferenceEngine.Core.Text;
using Microsoft.Extensions.Logging;

namespace SupportAssistant.Core.Agent
{
    /// <summary>
    /// Language-model service backed by the library's <see cref="TextGenerationEngine"/>
    /// (Phi-3-mini-4k-instruct). Generation is streamed token-by-token (so cancellation is honored
    /// mid-generation) and accumulated into the final text. The engine owns tokenization, model
    /// fetching, and the decode loop.
    /// </summary>
    /// <remarks>
    /// If the model cannot be loaded (for example the Phi-3 cpu-int4 export's <c>.onnx.data</c>
    /// external-data file is absent), the first generation fails and the service is marked
    /// unavailable so the orchestrator/RAG fallback path takes over without retrying on every call.
    /// </remarks>
    public class OnnxSLMService : ISLMService, IDisposable
    {
        private readonly TextGenerationEngine _engine;
        private readonly ILogger<OnnxSLMService>? _logger;

        // 0 = Unknown, 1 = Available, 2 = Unavailable. Probed lazily; cached after the first result.
        private int _state = (int)ProbeState.Unknown;

        private enum ProbeState { Unknown, Available, Unavailable }

        public OnnxSLMService(TextGenerationEngine engine, ILogger<OnnxSLMService>? logger = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _logger = logger;
        }

        /// <summary>True unless a previous generation proved the model cannot be loaded.</summary>
        public bool IsAvailable => (ProbeState)Interlocked.CompareExchange(ref _state, 0, 0) != ProbeState.Unavailable;

        public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if ((ProbeState)Interlocked.CompareExchange(ref _state, 0, 0) == ProbeState.Unavailable)
            {
                throw new InvalidOperationException("The language model is unavailable.");
            }

            var builder = new StringBuilder();
            await foreach (var chunk in _engine
                .PredictStreamingAsync(prompt, options: null, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(chunk);
            }

            Interlocked.Exchange(ref _state, (int)ProbeState.Available);
            return builder.ToString();
        }

        /// <summary>
        /// Forces a one-shot generation attempt and caches the availability outcome. Useful at
        /// startup to report whether the model loaded without paying the cost on the first user query.
        /// </summary>
        public async Task<bool> TryInitializeAsync(CancellationToken cancellationToken = default)
        {
            if ((ProbeState)Interlocked.CompareExchange(ref _state, 0, 0) == ProbeState.Available)
            {
                return true;
            }

            try
            {
                _ = await GenerateResponseAsync("hello", cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            (_engine as IDisposable)?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
