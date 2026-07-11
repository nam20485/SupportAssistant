using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using InferenceEngine.Core;
using InferenceEngine.Core.Text;
using SupportAssistant.Core.Engines;
using SupportAssistant.Core.Services;
using Xunit.Abstractions;

namespace SupportAssistant.Tests.Services;

/// <summary>
/// Real (non-mocked) end-to-end smoke test that exercises SupportAssistant's actual
/// <see cref="InferenceOptionsFactory"/> + <see cref="InferenceDiagnosticsService"/> wiring against a
/// real <see cref="TextEmbeddingEngine"/> session — the same construction path used by
/// <c>App.axaml.cs</c> for the production embedding engine. This is a hardware/host smoke test, not a
/// unit test: on a host without a usable ROCm/MIGraphX (or DirectML/CoreML) stack it still passes —
/// the assertions only require that the engine resolve *some* provider without throwing — but on a
/// capable host it prints the full ordered diagnostic trail, which is the "evidence" artifact this
/// test exists to produce (run with <c>dotnet test --filter FullyQualifiedName~RocmExecutionProviderSmokeTests -v n</c>
/// and read stdout, or set a breakpoint on the `engine.EngineInfo` line below in a debug session).
/// </summary>
public class RocmExecutionProviderSmokeTests
{
    private readonly ITestOutputHelper _output;

    public RocmExecutionProviderSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Embedding_RealSession_ReportsProviderAndCheckTrail()
    {
        // This intentionally does NOT gate on OperatingSystem.IsLinux()/File.Exists("/dev/kfd"): the
        // whole point is to observe what InferenceOptionsFactory.Create's real options (UseGpuAcceleration
        // = true, the same default the app ships) resolve to on *whatever* host runs this test. On
        // Windows that's DirectML, on macOS CoreML, on Linux MIGraphX-or-CPU-fallback — never a throw.
        //
        // WS5: allowGpuInProcess: true — this test runs in an isolated `dotnet test` console process
        // with no Mesa/GL context, so it cannot hit the Mesa/MIGraphX LLVM collision that
        // InferenceOptionsFactory.Create defaults to avoiding for the real GUI. See docs/plans/
        // inference-engine-integration-status.md §C. This opt-in is what keeps this test exercising
        // the real GPU path instead of just observing a forced CPU fallback.
        var diagnostics = new InferenceDiagnosticsService();
        var options = InferenceOptionsFactory.Create(
            settings: null,
            diagnostics: diagnostics,
            engineKind: "Embedding",
            allowGpuInProcess: true);

        // InferenceOptionsFactory doesn't accept a logger; construct the engine directly (mirrors
        // App.axaml.cs's `new TextEmbeddingEngine(options)` call) so OnSessionInitialized is exercised
        // exactly as in production.
        using var engine = new TextEmbeddingEngine(options);

        Exception? loadException = null;
        try
        {
            await engine.LoadAsync();
        }
        catch (Exception ex)
        {
            // A *managed* exception here is itself evidence (the library documents LoadAsync as
            // non-throwing/fallback-on-failure for recoverable native-load errors) — capture it instead
            // of letting the test framework swallow the stack trace.
            loadException = ex;
        }

        var info = engine.EngineInfo;

        _output.WriteLine("=================== ROCm / EP selection evidence ===================");
        _output.WriteLine($"Host OS               : {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        _output.WriteLine($"HSA_OVERRIDE_GFX_VERSION = {Environment.GetEnvironmentVariable("HSA_OVERRIDE_GFX_VERSION") ?? "(unset)"}");
        _output.WriteLine($"LoadAsync threw       : {(loadException is null ? "no" : loadException.GetType().FullName + ": " + loadException.Message)}");
        _output.WriteLine($"Provider              : {info.Provider}");
        _output.WriteLine($"IsFallback            : {info.IsFallback}");
        _output.WriteLine($"FallbackReason        : {info.FallbackReason}");
        _output.WriteLine($"IsRocmSessionActive   : {engine.IsRocmSessionActive}");
        _output.WriteLine($"Diagnostics.Embedding : Provider={diagnostics.Embedding.Provider} IsLoaded={diagnostics.Embedding.IsLoaded} IsFallback={diagnostics.Embedding.IsFallback}");
        _output.WriteLine("--- Checks trail (as surfaced to the Settings 'Acceleration Status' block) ---");
        foreach (var line in diagnostics.Embedding.Checks)
        {
            _output.WriteLine("  " + line);
        }
        _output.WriteLine("======================================================================");

        // Non-throwing contract: the library's documented behavior is "auto-detect and log, never
        // crash on missing GPU." A managed exception here means that contract was violated (distinct
        // from a *native* abort, which cannot be caught by any managed test and instead kills the test
        // host process outright — see docs/migraphx-rocblas-crash-troubleshooting.md upstream).
        loadException.Should().BeNull();

        // The diagnostics service must have received the OnSessionInitialized callback.
        diagnostics.Embedding.IsLoaded.Should().BeTrue();

        // Whatever provider was selected, the check trail must be non-empty and internally consistent.
        info.Checks.Should().NotBeEmpty();
        if (info.IsFallback)
        {
            info.Provider.ToString().Should().Be("Cpu");
            info.FallbackReason.ToString().Should().NotBe("None");
        }
        else
        {
            info.FallbackReason.ToString().Should().Be("None");
        }
    }
}
