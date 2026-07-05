using System.Collections.Generic;

namespace SupportAssistant.Core.Engines
{
    /// <summary>
    /// A consumer-side snapshot of the inference engine's runtime state, populated after
    /// <c>BaseInferenceEngine.LoadAsync</c>. InferenceEngine.Core exposes only a single
    /// <c>IsRocmSessionActive</c> bool (no <c>InferenceEngineInfo</c>/<c>InferenceFallbackReason</c>
    /// type exists upstream), so this type reconstructs a richer diagnostic for the Settings UI
    /// from the engine's public surface plus the configured options.
    /// </summary>
    public sealed class InferenceEngineDiagnostics
    {
        /// <summary>Human-readable execution provider actually in use (e.g. "ROCm", "DirectML", "CPU").</summary>
        public string Provider { get; init; } = "CPU";

        /// <summary>True if hardware acceleration was requested but the engine fell back to CPU.</summary>
        public bool IsFallback { get; init; }

        /// <summary>Why acceleration was unavailable, when known; otherwise null.</summary>
        public string? FallbackReason { get; init; }

        /// <summary>True when the ROCm execution provider is active (Linux/AMD GPU with ROCm userspace).</summary>
        public bool IsRocmActive { get; init; }

        /// <summary>Ordered human-readable checks (e.g. "GPU requested: true", "ROCm usable: false").</summary>
        public IReadOnlyList<string> Checks { get; init; } = System.Array.Empty<string>();
    }
}
