using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SupportAssistant.Core.Engines;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Tests.Engines;

public class RocmHostPreflightTests
{
    [Fact]
    public void Evaluate_WhenOverrideMissingForGfx1031_AndStackPresent_IsCompatibleExceptOverride()
    {
        var result = RocmHostPreflight.Evaluate(
            getEnvironmentVariable: _ => null,
            readGfxTargetVersions: () => new[] { 100301 },
            isRocmStackPresent: () => true);

        result.Status.Should().Be(RocmPreflightStatus.OverrideRequired);
        result.IsCompatibleExceptOverride.Should().BeTrue();
        result.RequiresCpuFallback.Should().BeTrue();
        result.LegacyArchNames.Should().ContainSingle().Which.Should().Be("gfx1031");
        result.Message.Should().Contain(RocmHostPreflight.OverrideEnvironmentVariable);
        result.Message.Should().Contain(RocmHostPreflight.RecommendedOverrideValue);
    }

    [Fact]
    public void Evaluate_WhenOverrideMissingAndStackIncomplete_IsIncompleteRuntime()
    {
        var result = RocmHostPreflight.Evaluate(
            getEnvironmentVariable: _ => null,
            readGfxTargetVersions: () => new[] { 100301 },
            isRocmStackPresent: () => false);

        result.Status.Should().Be(RocmPreflightStatus.IncompleteRuntime);
        result.IsCompatibleExceptOverride.Should().BeFalse();
        result.RequiresCpuFallback.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WhenOverridePresentForGfx1031_IsOk()
    {
        var result = RocmHostPreflight.Evaluate(
            getEnvironmentVariable: name =>
                name == RocmHostPreflight.OverrideEnvironmentVariable ? "10.3.0" : null,
            readGfxTargetVersions: () => new[] { 100301 },
            isRocmStackPresent: () => true);

        result.Status.Should().Be(RocmPreflightStatus.Ok);
        result.IsCompatibleExceptOverride.Should().BeFalse();
        result.RequiresCpuFallback.Should().BeFalse();
        result.Message.Should().Contain("10.3.0");
    }

    [Fact]
    public void Evaluate_WhenGfx1030Only_IsOkWithoutOverride()
    {
        var result = RocmHostPreflight.Evaluate(
            getEnvironmentVariable: _ => null,
            readGfxTargetVersions: () => new[] { 100300 },
            isRocmStackPresent: () => true);

        result.Status.Should().Be(RocmPreflightStatus.Ok);
        result.RequiresCpuFallback.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_WhenNoGpuNodes_IsNotApplicable()
    {
        var result = RocmHostPreflight.Evaluate(
            getEnvironmentVariable: _ => null,
            readGfxTargetVersions: () => Array.Empty<int>(),
            isRocmStackPresent: () => true);

        result.Status.Should().Be(RocmPreflightStatus.NotApplicable);
        result.RequiresCpuFallback.Should().BeFalse();
    }

    [Theory]
    [InlineData(100301, "gfx1031")]
    [InlineData(100300, "gfx1030")]
    [InlineData(110000, "gfx1100")]
    public void FormatGfxName_MatchesFamiliarIsaNames(int version, string expected)
    {
        RocmHostPreflight.FormatGfxName(version).Should().Be(expected);
    }

    [Fact]
    public void InferenceOptionsFactory_DisablesGpu_WhenOverrideRequired()
    {
        var preflight = new RocmPreflightResult
        {
            Status = RocmPreflightStatus.OverrideRequired,
            Message = "test override required",
            GfxTargetVersions = new[] { 100301 },
            LegacyArchNames = new[] { "gfx1031" },
        };

        var options = InferenceOptionsFactory.Create(
            settings: null,
            rocmPreflight: preflight);

        options.UseGpuAcceleration.Should().BeFalse();
        InferenceOptionsFactory.LastRocmPreflight.Should().BeSameAs(preflight);
    }

    [Fact]
    public void InferenceOptionsFactory_KeepsGpu_WhenPreflightOk_AndGpuExplicitlyAllowedInProcess()
    {
        var preflight = new RocmPreflightResult
        {
            Status = RocmPreflightStatus.Ok,
            Message = "ok",
            GfxTargetVersions = new[] { 100301 },
        };

        // WS5: allowGpuInProcess: true mirrors RocmExecutionProviderSmokeTests' opt-in — isolates
        // this assertion (ROCm preflight says Ok -> GPU stays on) from the separately-tested
        // GUI-safety branch below, which would otherwise force CPU on Linux regardless of preflight.
        var options = InferenceOptionsFactory.Create(
            settings: null,
            rocmPreflight: preflight,
            allowGpuInProcess: true);

        options.UseGpuAcceleration.Should().BeTrue();
        InferenceOptionsFactory.LastForcedCpuForGuiSafety.Should().BeFalse();
    }

    // WS5: interim mitigation (docs/plans/inference-engine-integration-status.md §C): on Linux,
    // in-process GPU (ROCm/MIGraphX, which links comgr's LLVM) collides with the GUI's Mesa-linked
    // LLVM and hard-aborts the process. InferenceOptionsFactory.Create's allowGpuInProcess defaults
    // to false so every production in-process caller is safe by default with zero call-site changes.

    [Fact]
    public void InferenceOptionsFactory_ForcesCpu_OnLinux_WhenPreflightOk_AndGpuNotAllowedInProcess()
    {
        var preflight = new RocmPreflightResult
        {
            Status = RocmPreflightStatus.Ok,
            Message = "ok",
            GfxTargetVersions = new[] { 100301 },
        };

        // Default allowGpuInProcess (false) — matches every production call site in App.axaml.cs,
        // QueryProcessingService, and KnowledgeBaseService.
        var options = InferenceOptionsFactory.Create(
            settings: null,
            rocmPreflight: preflight);

        if (OperatingSystem.IsLinux())
        {
            options.UseGpuAcceleration.Should().BeFalse();
            InferenceOptionsFactory.LastForcedCpuForGuiSafety.Should().BeTrue();
        }
        else
        {
            // Only Linux's Mesa<->MIGraphX/comgr LLVM linkage causes the collision; other platforms
            // (Windows/DirectML, macOS/CoreML) are unaffected by this interim mitigation.
            options.UseGpuAcceleration.Should().BeTrue();
            InferenceOptionsFactory.LastForcedCpuForGuiSafety.Should().BeFalse();
        }
    }

    [Fact]
    public void InferenceOptionsFactory_DisablesGpu_WhenOverrideRequired_EvenWhenGpuAllowedInProcess()
    {
        var preflight = new RocmPreflightResult
        {
            Status = RocmPreflightStatus.OverrideRequired,
            Message = "test override required",
            GfxTargetVersions = new[] { 100301 },
            LegacyArchNames = new[] { "gfx1031" },
        };

        // WS5: allowGpuInProcess only bypasses the GUI-safety branch; it must not bypass the
        // independent legacy-ISA-without-override safety (that one is a rocBLAS SIGABRT, not the
        // Mesa/MIGraphX LLVM collision, and applies regardless of process type).
        var options = InferenceOptionsFactory.Create(
            settings: null,
            rocmPreflight: preflight,
            allowGpuInProcess: true);

        options.UseGpuAcceleration.Should().BeFalse();
        InferenceOptionsFactory.LastForcedCpuForGuiSafety.Should().BeFalse();
    }
}

public class LegacyRocmGpuPromptTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _settings;

    public LegacyRocmGpuPromptTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sa-legacy-prompt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settings = new SettingsService(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static RocmPreflightResult CompatibleExceptOverride() => new()
    {
        Status = RocmPreflightStatus.OverrideRequired,
        Message = "compatible except override",
        GfxTargetVersions = new[] { 100301 },
        LegacyArchNames = new[] { "gfx1031" },
    };

    [Fact]
    public void ShouldPrompt_WhenCompatibleExceptOverride_AndGpuRequested()
    {
        _settings.Settings.Ai.ExecutionProvider = "ROCm";
        _settings.Settings.Ai.BypassLegacyRocmGpuDialog = false;

        LegacyRocmGpuPrompt.ShouldPrompt(CompatibleExceptOverride(), _settings).Should().BeTrue();
    }

    [Fact]
    public void ShouldPrompt_False_WhenBypassSettingEnabled()
    {
        _settings.Settings.Ai.ExecutionProvider = "ROCm";
        _settings.Settings.Ai.BypassLegacyRocmGpuDialog = true;

        LegacyRocmGpuPrompt.ShouldPrompt(CompatibleExceptOverride(), _settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldPrompt_False_WhenExecutionProviderIsCpu()
    {
        _settings.Settings.Ai.ExecutionProvider = "CPU";
        _settings.Settings.Ai.BypassLegacyRocmGpuDialog = false;

        LegacyRocmGpuPrompt.ShouldPrompt(CompatibleExceptOverride(), _settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldPrompt_False_WhenNotCompatibleExceptOverride()
    {
        _settings.Settings.Ai.ExecutionProvider = "ROCm";
        var preflight = new RocmPreflightResult
        {
            Status = RocmPreflightStatus.IncompleteRuntime,
            Message = "incomplete",
        };

        LegacyRocmGpuPrompt.ShouldPrompt(preflight, _settings).Should().BeFalse();
    }

    [Fact]
    public async Task BypassSetting_RoundTripsThroughSettingsService()
    {
        _settings.Settings.Ai.BypassLegacyRocmGpuDialog = true;
        await _settings.SaveSettingsAsync();

        var reloaded = new SettingsService(_tempDir);
        await reloaded.LoadSettingsAsync();

        reloaded.Settings.Ai.BypassLegacyRocmGpuDialog.Should().BeTrue();
    }
}
