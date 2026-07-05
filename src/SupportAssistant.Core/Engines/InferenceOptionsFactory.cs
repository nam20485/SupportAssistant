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
        /// select the CPU provider.
        /// </summary>
        public static InferenceEngineOptions Create(ISettingsService? settings)
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

            return new InferenceEngineOptions
            {
                UseGpuAcceleration = useGpu,
                DeviceId = 0,
                WarmupOnLoad = true
            };
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
