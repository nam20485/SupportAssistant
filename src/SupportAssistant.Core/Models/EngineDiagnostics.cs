using System.Collections.Generic;

namespace SupportAssistant.Core.Models;

/// <summary>
/// UI-facing, library-decoupled snapshot of one engine's loaded provider and fallback trail.
/// The app layer binds to this DTO rather than the library's <c>InferenceEngineInfo</c> struct,
/// keeping the UI edge decoupled from <c>InferenceEngine.Core</c> (plan guiding principle #5).
/// </summary>
public sealed record EngineDiagnostics
{
    /// <summary>"Embedding" or "Generation".</summary>
    public string EngineKind { get; init; } = string.Empty;

    /// <summary>The execution provider that actually loaded (e.g. "DirectML", "MIGraphX", "Cpu").</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>True when the engine fell back away from the requested provider.</summary>
    public bool IsFallback { get; init; }

    /// <summary>Empty on success; otherwise a short human reason (the fallback enum name).</summary>
    public string FallbackReason { get; init; } = string.Empty;

    /// <summary>Ordered, human-readable check lines (e.g. "rocm.append: Failed — ...").</summary>
    public IReadOnlyList<string> Checks { get; init; } = System.Array.Empty<string>();

    /// <summary>True once the first <c>OnSessionInitialized</c> fires for this engine.</summary>
    public bool IsLoaded { get; init; }
}
