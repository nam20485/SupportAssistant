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
        /// Creates base options. Hardware acceleration is enabled unless the settings explicitly
        /// select the CPU provider. When <paramref name="diagnostics"/> is the concrete
        /// <see cref="InferenceDiagnosticsService"/>, the library's
        /// <see cref="InferenceEngineOptions.OnSessionInitialized"/> callback is attached so the
        /// resolved provider + fallback trail is surfaced to the UI.
        /// </summary>
        /// <param name="settings">Application settings (null tolerates settings-free construction).</param>
        /// <param name="diagnostics">Diagnostics sink (null = no callback attached).</param>
        /// <param name="engineKind">"Embedding" or "Generation" — labels the published snapshot.</param>
        public static InferenceEngineOptions Create(
            ISettingsService? settings,
            IInferenceDiagnosticsService? diagnostics = null,
            string engineKind = "Generation")
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
