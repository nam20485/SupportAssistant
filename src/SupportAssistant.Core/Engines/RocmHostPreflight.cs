using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SupportAssistant.Core.Engines;

/// <summary>
/// Outcome of a Linux/ROCm host preflight check for AMD GPUs that need
/// <c>HSA_OVERRIDE_GFX_VERSION</c> before MIGraphX/rocBLAS can run safely.
/// </summary>
public enum RocmPreflightStatus
{
    /// <summary>Not a Linux/ROCm host, or no relevant GPU nodes were found.</summary>
    NotApplicable,

    /// <summary>Host is fine for ROCm GPU inference (or the required override is already set).</summary>
    Ok,

    /// <summary>
    /// Stack looks ROCm-ready and the GPU is a supported-but-legacy ISA, but
    /// <c>HSA_OVERRIDE_GFX_VERSION</c> is unset. Proceeding with GPU will hard-abort (SIGABRT).
    /// </summary>
    OverrideRequired,

    /// <summary>
    /// A legacy ISA was detected but the ROCm userspace stack is incomplete
    /// (missing KFD device and/or HIP/MIGraphX libraries).
    /// </summary>
    IncompleteRuntime,
}

/// <summary>
/// Result of <see cref="RocmHostPreflight.Evaluate"/>.
/// </summary>
public sealed class RocmPreflightResult
{
    public RocmPreflightStatus Status { get; init; }

    /// <summary>Human-readable explanation suitable for logs / stderr / dialogs.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Raw <c>gfx_target_version</c> values read from KFD (e.g. 100301 = gfx1031).</summary>
    public IReadOnlyList<int> GfxTargetVersions { get; init; } = Array.Empty<int>();

    /// <summary>
    /// True when the host is otherwise ROCm-ready and the only blocker is a missing
    /// launch-time <c>HSA_OVERRIDE_GFX_VERSION</c> for a legacy ISA.
    /// </summary>
    public bool IsCompatibleExceptOverride => Status == RocmPreflightStatus.OverrideRequired;

    public bool RequiresCpuFallback =>
        Status is RocmPreflightStatus.OverrideRequired or RocmPreflightStatus.IncompleteRuntime;

    /// <summary>Friendly ISA names for UI (e.g. gfx1031).</summary>
    public IReadOnlyList<string> LegacyArchNames { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Detects AMD RDNA2 ISAs (gfx1031/1032/1034) that crash rocBLAS unless the process was launched
/// with <c>HSA_OVERRIDE_GFX_VERSION=10.3.0</c>. The override must be present at process start —
/// setting it via <see cref="Environment.SetEnvironmentVariable(string, string?)"/> inside
/// <c>Main</c> is too late (HSA/ROCm natives ignore a late change).
/// </summary>
public static class RocmHostPreflight
{
    public const string OverrideEnvironmentVariable = "HSA_OVERRIDE_GFX_VERSION";
    public const string RecommendedOverrideValue = "10.3.0";

    /// <summary>
    /// KFD <c>gfx_target_version</c> encodings for ISAs that lack rocBLAS Tensile kernels in
    /// current ROCm packages (kernels ship for gfx1030 / 100300, not these variants).
    /// Encoding: major*10000 + minor*100 + stepping → gfx1031 = 100301.
    /// </summary>
    private static readonly HashSet<int> ArchitecturesRequiringOverride = new()
    {
        100301, // gfx1031 (e.g. RX 6700 XT)
        100302, // gfx1032
        100304, // gfx1034
    };

    private static readonly string[] HipLibraryCandidates =
    {
        "/opt/rocm/lib/libamdhip64.so.7",
        "/opt/rocm/lib/libamdhip64.so",
        "/usr/lib/libamdhip64.so.7",
        "/usr/lib/x86_64-linux-gnu/libamdhip64.so.7",
    };

    private static readonly string[] MigraphxLibraryCandidates =
    {
        "/opt/rocm/lib/libmigraphx_c.so.3",
        "/opt/rocm/lib/libmigraphx_c.so",
        "/usr/lib/libmigraphx_c.so.3",
        "/usr/lib/x86_64-linux-gnu/libmigraphx_c.so.3",
    };

    private const string KfdDevicePath = "/dev/kfd";
    private const string KfdTopologyNodesPath = "/sys/class/kfd/kfd/topology/nodes";

    /// <summary>
    /// Evaluates whether this process can safely attempt ROCm/MIGraphX GPU inference.
    /// </summary>
    /// <param name="getEnvironmentVariable">Optional env reader (tests).</param>
    /// <param name="readGfxTargetVersions">Optional KFD gfx version reader (tests).</param>
    /// <param name="isRocmStackPresent">
    /// Optional stack probe (tests). Default checks <c>/dev/kfd</c> plus HIP/MIGraphX libraries.
    /// </param>
    public static RocmPreflightResult Evaluate(
        Func<string, string?>? getEnvironmentVariable = null,
        Func<IReadOnlyList<int>>? readGfxTargetVersions = null,
        Func<bool>? isRocmStackPresent = null)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new RocmPreflightResult
            {
                Status = RocmPreflightStatus.NotApplicable,
                Message = "ROCm HSA override preflight applies only on Linux.",
            };
        }

        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        readGfxTargetVersions ??= ReadGfxTargetVersionsFromSysfs;
        isRocmStackPresent ??= IsDefaultRocmStackPresent;

        var versions = readGfxTargetVersions() ?? Array.Empty<int>();
        var needingOverride = versions.Where(v => ArchitecturesRequiringOverride.Contains(v)).ToArray();
        if (needingOverride.Length == 0)
        {
            return new RocmPreflightResult
            {
                Status = versions.Count == 0 ? RocmPreflightStatus.NotApplicable : RocmPreflightStatus.Ok,
                Message = versions.Count == 0
                    ? "No KFD GPU nodes with a gfx_target_version were found."
                    : $"KFD GPU ISA(s) do not require {OverrideEnvironmentVariable}: {FormatGfxList(versions)}.",
                GfxTargetVersions = versions,
            };
        }

        var legacyNames = needingOverride.Select(FormatGfxName).Distinct(StringComparer.Ordinal).ToArray();
        var overrideValue = getEnvironmentVariable(OverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return new RocmPreflightResult
            {
                Status = RocmPreflightStatus.Ok,
                Message =
                    $"{OverrideEnvironmentVariable}={overrideValue} is set; " +
                    $"GPU ISA(s) {FormatGfxList(needingOverride)} can use the gfx1030 rocBLAS kernels.",
                GfxTargetVersions = versions,
                LegacyArchNames = legacyNames,
            };
        }

        if (!isRocmStackPresent())
        {
            return new RocmPreflightResult
            {
                Status = RocmPreflightStatus.IncompleteRuntime,
                Message =
                    $"AMD GPU ISA {FormatGfxList(needingOverride)} needs {OverrideEnvironmentVariable}=" +
                    $"{RecommendedOverrideValue}, but the ROCm runtime stack is incomplete " +
                    "(missing /dev/kfd and/or HIP/MIGraphX libraries). GPU acceleration will be disabled.",
                GfxTargetVersions = versions,
                LegacyArchNames = legacyNames,
            };
        }

        return new RocmPreflightResult
        {
            Status = RocmPreflightStatus.OverrideRequired,
            Message =
                $"Host is ROCm-compatible except for a missing launch-time " +
                $"{OverrideEnvironmentVariable}={RecommendedOverrideValue} " +
                $"(legacy ISA {FormatGfxList(needingOverride)}). " +
                "Launching MIGraphX without it hard-aborts the process (SIGABRT). " +
                "Set the variable in the launching environment and restart — setting it inside Main() is too late.",
            GfxTargetVersions = versions,
            LegacyArchNames = legacyNames,
        };
    }

    /// <summary>
    /// Reads <c>gfx_target_version</c> from every KFD topology node. Sysfs reports the real ISA
    /// even when <c>HSA_OVERRIDE_GFX_VERSION</c> is set (unlike <c>rocminfo</c>).
    /// </summary>
    public static IReadOnlyList<int> ReadGfxTargetVersionsFromSysfs()
    {
        if (!Directory.Exists(KfdTopologyNodesPath))
        {
            return Array.Empty<int>();
        }

        var versions = new List<int>();
        foreach (var nodeDir in Directory.EnumerateDirectories(KfdTopologyNodesPath))
        {
            var propertiesPath = Path.Combine(nodeDir, "properties");
            if (!File.Exists(propertiesPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(propertiesPath))
            {
                if (!line.StartsWith("gfx_target_version", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
                    && version > 0)
                {
                    versions.Add(version);
                }
            }
        }

        return versions;
    }

    /// <summary>
    /// True when <c>/dev/kfd</c> exists and at least one HIP + MIGraphX C API library is on disk.
    /// </summary>
    public static bool IsDefaultRocmStackPresent()
        => File.Exists(KfdDevicePath)
           && HipLibraryCandidates.Any(File.Exists)
           && MigraphxLibraryCandidates.Any(File.Exists);

    private static string FormatGfxList(IReadOnlyList<int> versions)
        => string.Join(", ", versions.Select(FormatGfxName));

    /// <summary>Formats KFD gfx_target_version as a familiar name (100301 → gfx1031).</summary>
    public static string FormatGfxName(int gfxTargetVersion)
    {
        var major = gfxTargetVersion / 10000;
        var minor = (gfxTargetVersion / 100) % 100;
        var stepping = gfxTargetVersion % 100;
        return $"gfx{major}{minor}{stepping}";
    }
}
