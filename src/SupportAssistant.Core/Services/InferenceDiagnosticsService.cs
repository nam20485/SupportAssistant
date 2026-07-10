using System;
using System.Linq;
using System.Threading;
using InferenceEngine.Core.Diagnostics;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Thread-safe implementation of <see cref="IInferenceDiagnosticsService"/>. Snapshots are
/// immutable records; the swap and event raise are guarded by a lock. <see cref="Report"/>
/// is invoked from the library's <c>OnSessionInitialized</c> callback, which runs on a
/// thread-pool thread.
/// </summary>
public sealed class InferenceDiagnosticsService : IInferenceDiagnosticsService
{
    private readonly object _gate = new();
    private EngineDiagnostics _embedding = Empty("Embedding");
    private EngineDiagnostics _generation = Empty("Generation");

    /// <inheritdoc />
    public EngineDiagnostics Embedding { get { lock (_gate) return _embedding; } }

    /// <inheritdoc />
    public EngineDiagnostics Generation { get { lock (_gate) return _generation; } }

    /// <inheritdoc />
    public event EventHandler? Updated;

    /// <summary>
    /// Called from <c>OnSessionInitialized</c> (thread-pool thread) to publish the library's
    /// <see cref="InferenceEngineInfo"/> for one engine as a library-decoupled snapshot.
    /// </summary>
    /// <param name="engineKind">"Embedding" or "Generation".</param>
    /// <param name="info">The library's resolved provider + fallback trail.</param>
    public void Report(string engineKind, InferenceEngineInfo info)
    {
        var snapshot = Map(engineKind, info);
        EventHandler? handler;
        lock (_gate)
        {
            if (engineKind == "Embedding")
            {
                _embedding = snapshot;
            }
            else
            {
                _generation = snapshot;
            }

            handler = Updated;
        }

        handler?.Invoke(this, EventArgs.Empty);
    }

    private static EngineDiagnostics Map(string kind, InferenceEngineInfo i) => new()
    {
        EngineKind = kind,
        Provider = i.Provider.ToString(),
        IsFallback = i.IsFallback,
        FallbackReason = i.FallbackReason == InferenceFallbackReason.None ? "" : i.FallbackReason.ToString(),
        Checks = i.Checks
            .Select(c => $"{c.Name}: {c.Result}" + (string.IsNullOrEmpty(c.Detail) ? "" : $" — {c.Detail}"))
            .ToList(),
        IsLoaded = true,
    };

    private static EngineDiagnostics Empty(string kind) => new() { EngineKind = kind };
}
