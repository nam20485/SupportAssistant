using System;
using System.IO;
using InferenceEngine.Core;
using InferenceEngine.Core.ModelProviders;
using Microsoft.Extensions.Logging;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Core.Engines
{
    /// <summary>
    /// Builds <see cref="InferenceEngineOptions"/> from application settings and resolves the model
    /// for an engine. Centralizes the mapping described in Phase 4 Stage 1 (GPU toggle →
    /// <see cref="InferenceEngineOptions.UseGpuAcceleration"/>, <see cref="InferenceEngineOptions.DeviceId"/>,
    /// <see cref="InferenceEngineOptions.WarmupOnLoad"/>).
    /// </summary>
    public static class InferenceOptionsFactory
    {
        /// <summary>
        /// Creates base options. Hardware acceleration is enabled unless the settings explicitly
        /// select the CPU provider.
        /// </summary>
        public static InferenceEngineOptions Create(ISettingsService? settings)
        {
            var useGpu = true;
            var providerName = "Auto";
            if (settings != null)
            {
                providerName = settings.Settings.Ai.ExecutionProvider?.Trim() ?? string.Empty;
                if (providerName.Equals("CPU", StringComparison.OrdinalIgnoreCase))
                {
                    useGpu = false;
                }
            }

            return new InferenceEngineOptions
            {
                UseGpuAcceleration = useGpu,
                DeviceId = 0,
                WarmupOnLoad = true
            };
        }

        /// <summary>
        /// Returns a configured <see cref="InferenceEngineOptions"/> that points at <paramref name="modelPath"/>
        /// via a <see cref="LocalModelProvider"/> when the file exists, enabling offline/bundled use.
        /// When the file is absent the provider is left null so the engine reports unavailable (no
        /// automatic network download is attempted, keeping first-run/CI deterministic).
        /// </summary>
        public static InferenceEngineOptions WithLocalModel(this InferenceEngineOptions options, string? modelPath)
        {
            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                options.ModelPath = modelPath;
            }

            return options;
        }

        /// <summary>
        /// Builds a <see cref="InferenceEngineDiagnostics"/> snapshot from an engine's public state.
        /// </summary>
        public static InferenceEngineDiagnostics BuildDiagnostics<TIn, TOut>(
            BaseInferenceEngine<TIn, TOut> engine,
            InferenceEngineOptions options,
            ILogger? logger,
            string? loadError = null)
        {
            var checks = new System.Collections.Generic.List<string>
            {
                $"GPU requested: {options.UseGpuAcceleration}",
                $"Device id: {options.DeviceId}",
                $"ROCm session active: {engine.IsRocmSessionActive}"
            };

            var isRocm = engine.IsRocmSessionActive;
            var provider = isRocm ? "ROCm" : (OperatingSystem.IsWindows() ? "DirectML/CPU" : "CPU");

            var isFallback = options.UseGpuAcceleration && !isRocm && !OperatingSystem.IsWindows();
            string? reason = isFallback ? (loadError ?? "GPU acceleration requested but CPU session in use") : loadError;

            if (loadError != null)
            {
                checks.Add($"Load error: {loadError}");
            }

            return new InferenceEngineDiagnostics
            {
                Provider = provider,
                IsFallback = isFallback,
                FallbackReason = reason,
                IsRocmActive = isRocm,
                Checks = checks
            };
        }
    }
}
