using System;
using System.IO;
using InferenceEngine.Core;
using InferenceEngine.Core.ModelProviders;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Core.Engines
{
    /// <summary>
    /// Builds <see cref="InferenceEngineOptions"/> from application settings and resolves a local
    /// model path. Centralizes the mapping described in Phase 4 Stage 1 (GPU toggle →
    /// <see cref="InferenceEngineOptions.UseGpuAcceleration"/>, <see cref="InferenceEngineOptions.DeviceId"/>,
    /// <see cref="InferenceEngineOptions.WarmupOnLoad"/>). Runtime diagnostics now come from the
    /// library's <c>InferenceEngineInfo</c> (<see cref="BaseInferenceEngine{TInput,TOutput}.EngineInfo"/>).
    /// </summary>
    public static class InferenceOptionsFactory
    {
        /// <summary>
        /// Last ROCm host preflight result consulted while building options (null until first
        /// <see cref="Create"/> call that evaluates the host). Useful for startup logging.
        /// </summary>
        public static RocmPreflightResult? LastRocmPreflight { get; private set; }

        /// <summary>
        /// True when the most recent <see cref="Create"/> call forced CPU for the WS5: in-process-GUI
        /// safety reason below (distinct from <see cref="LastRocmPreflight"/>'s legacy-ISA reason).
        /// Read by <c>SettingsViewModel</c> to show accurate acceleration-status text.
        /// </summary>
        public static bool LastForcedCpuForGuiSafety { get; private set; }

        /// <summary>
        /// Creates base options. Hardware acceleration is enabled unless the settings explicitly
        /// select the CPU provider, a Linux/ROCm host preflight detects a GPU ISA that would SIGABRT
        /// without <c>HSA_OVERRIDE_GFX_VERSION</c> (see <see cref="RocmHostPreflight"/>), or — WS5:
        /// interim safety, see below — this is an in-process GUI host on Linux.
        /// When <paramref name="diagnostics"/> is the concrete
        /// <see cref="InferenceDiagnosticsService"/>, the library's
        /// <see cref="InferenceEngineOptions.OnSessionInitialized"/> callback is attached so the
        /// resolved provider + fallback trail is surfaced to the UI.
        /// </summary>
        /// <param name="settings">Application settings (null tolerates settings-free construction).</param>
        /// <param name="diagnostics">Diagnostics sink (null = no callback attached).</param>
        /// <param name="engineKind">"Embedding" or "Generation" — labels the published snapshot.</param>
        /// <param name="rocmPreflight">
        /// Optional preflight result (tests). When null, <see cref="RocmHostPreflight.Evaluate"/> runs.
        /// </param>
        /// <param name="allowGpuInProcess">
        /// WS5: interim mitigation (see docs/plans/inference-engine-integration-status.md §C). On Linux,
        /// GPU acceleration (ROCm/MIGraphX, which links its own LLVM via comgr) collides with the GUI's
        /// Mesa-linked LLVM and hard-aborts the process — a native crash, not catchable. Real fix is the
        /// out-of-process worker (WS5); until then this defaults to <c>false</c> so every in-process
        /// caller (the GUI's real embedding/generation engines in <c>App.axaml.cs</c>) is safe by
        /// default with zero call-site changes. Isolated, non-GUI callers (e.g.
        /// <c>RocmExecutionProviderSmokeTests</c>, run in a separate console process with no Mesa/GL
        /// context) must opt in explicitly to exercise the real GPU path.
        /// </param>
        public static InferenceEngineOptions Create(
            ISettingsService? settings,
            IInferenceDiagnosticsService? diagnostics = null,
            string engineKind = "Generation",
            RocmPreflightResult? rocmPreflight = null,
            bool allowGpuInProcess = false)
        {
            var useGpu = true;
            if (settings != null)
            {
                var providerName = settings.Settings.Ai.ExecutionProvider?.Trim() ?? string.Empty;
                if (providerName.Equals("CPU", StringComparison.OrdinalIgnoreCase))
                {
                    useGpu = false;
                }
            }

            LastForcedCpuForGuiSafety = false;

            // Never attempt MIGraphX on gfx1031/1032/1034 without the launch-time HSA override —
            // rocBLAS aborts the whole process, which managed CPU fallback cannot catch.
            if (useGpu)
            {
                LastRocmPreflight = rocmPreflight ?? RocmHostPreflight.Evaluate();
                if (LastRocmPreflight.RequiresCpuFallback)
                {
                    useGpu = false;
                    Console.Error.WriteLine(LastRocmPreflight.Message);
                }
                else if (OperatingSystem.IsLinux() && !allowGpuInProcess)
                {
                    // WS5: interim mitigation — force CPU rather than risk the Mesa/MIGraphX LLVM
                    // CommandLine collision (reproduced 2026-07-10 — see docs/plans/
                    // inference-engine-integration-status.md §C). Remove once the out-of-process
                    // worker lands and this construction site routes through it instead.
                    useGpu = false;
                    LastForcedCpuForGuiSafety = true;
                    Console.Error.WriteLine(
                        "[WS5] Forcing CPU: in-process GPU acceleration is disabled on Linux to avoid " +
                        "the Mesa/MIGraphX LLVM CommandLine collision (native, unrecoverable abort). " +
                        "See docs/plans/inference-engine-integration-status.md §C.");
                }
            }

            var options = new InferenceEngineOptions
            {
                UseGpuAcceleration = useGpu,
                DeviceId = 0,
                WarmupOnLoad = true
            };

            // The callback fires once, on a thread-pool thread, after the session is built
            // (including fallback). Report() is thread-safe and marshals nothing itself; the
            // UI consumer of IInferenceDiagnosticsService.Updated is responsible for UI-thread marshaling.
            if (diagnostics is InferenceDiagnosticsService svc)
            {
                options.OnSessionInitialized = info => svc.Report(engineKind, info);
            }

            return options;
        }

        /// <summary>
        /// Returns the configured <see cref="InferenceEngineOptions"/> with <paramref name="modelPath"/>
        /// applied via a <see cref="LocalModelProvider"/> when the file exists (offline/bundled use).
        /// When the file is absent the provider is left null so the library's download URLs are used
        /// (the text engines fetch their own models + tokenizer assets).
        /// </summary>
        public static InferenceEngineOptions WithLocalModel(this InferenceEngineOptions options, string? modelPath)
        {
            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                options.ModelPath = modelPath;
            }

            return options;
        }
    }
}
