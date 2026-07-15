using System;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Provides UI-facing snapshots of which execution provider each inference engine actually
/// loaded, plus the reason for any CPU fallback. The snapshots are library-decoupled
/// (<see cref="EngineDiagnostics"/>); consumers never reference <c>InferenceEngine.Core</c>.
/// </summary>
public interface IInferenceDiagnosticsService
{
    /// <summary>Snapshot of the embedding engine (not loaded until <see cref="EngineDiagnostics.IsLoaded"/>).</summary>
    EngineDiagnostics Embedding { get; }

    /// <summary>Snapshot of the generation engine (not loaded until <see cref="EngineDiagnostics.IsLoaded"/>).</summary>
    EngineDiagnostics Generation { get; }

    /// <summary>
    /// Raised (on a thread-pool thread) whenever either engine reports a load or fallback.
    /// UI consumers must marshal to the UI thread.
    /// </summary>
    event EventHandler? Updated;
}
