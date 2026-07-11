using System;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Core.Engines;

/// <summary>
/// Decides whether to show the legacy-AMD-GPU / missing-<c>HSA_OVERRIDE_GFX_VERSION</c> prompt.
/// </summary>
public static class LegacyRocmGpuPrompt
{
    /// <summary>
    /// Returns true when the host is ROCm-ready except for the launch-time override, the user has
    /// not opted into CPU-only bypass, and settings are not already forcing CPU.
    /// </summary>
    public static bool ShouldPrompt(RocmPreflightResult preflight, ISettingsService? settings)
    {
        ArgumentNullException.ThrowIfNull(preflight);

        if (!preflight.IsCompatibleExceptOverride)
        {
            return false;
        }

        if (settings == null)
        {
            return true;
        }

        if (settings.Settings.Ai.BypassLegacyRocmGpuDialog)
        {
            return false;
        }

        var provider = settings.Settings.Ai.ExecutionProvider?.Trim() ?? string.Empty;
        if (provider.Equals("CPU", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
