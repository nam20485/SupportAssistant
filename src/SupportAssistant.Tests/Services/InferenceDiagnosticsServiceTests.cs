using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using InferenceEngine.Core.Diagnostics;
using SupportAssistant.Core.Models;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Tests.Services;

public class InferenceDiagnosticsServiceTests
{
    [Fact]
    public void Report_MapsFallbackSnapshotCorrectly()
    {
        // Arrange — a generation engine that fell back to CPU because the MIGraphX runtime is missing.
        var service = new InferenceDiagnosticsService();
        var info = new InferenceEngineInfo
        {
            Provider = InferenceProvider.Cpu,
            IsFallback = true,
            FallbackReason = InferenceFallbackReason.MIGraphXRuntimeMissing,
            Checks = new[]
            {
                ProviderDiagnosticCheck.Fail("migraphx.load", "Load libmigraphx", "Provider", 0, "libmigraphx_c.so.3 missing"),
            },
        };

        // Act
        service.Report("Generation", info);

        // Assert
        var snap = service.Generation;
        snap.EngineKind.Should().Be("Generation");
        snap.Provider.Should().Be("Cpu");
        snap.IsFallback.Should().BeTrue();
        snap.FallbackReason.Should().NotBeNullOrEmpty();
        snap.FallbackReason.Should().Be(nameof(InferenceFallbackReason.MIGraphXRuntimeMissing));
        snap.IsLoaded.Should().BeTrue();
        snap.Checks.Should().ContainSingle();
        snap.Checks[0].Should().Contain("migraphx.load");
        snap.Checks[0].Should().Contain("Failed");
        snap.Checks[0].Should().Contain("libmigraphx_c.so.3 missing");
    }

    [Fact]
    public void Report_NonFallbackSnapshot_HasEmptyReason()
    {
        // Arrange — a successful DirectML load (no fallback).
        var service = new InferenceDiagnosticsService();
        var info = new InferenceEngineInfo
        {
            Provider = InferenceProvider.DirectML,
            IsFallback = false,
            FallbackReason = InferenceFallbackReason.None,
        };

        // Act
        service.Report("Embedding", info);

        // Assert
        var snap = service.Embedding;
        snap.Provider.Should().Be("DirectML");
        snap.IsFallback.Should().BeFalse();
        snap.FallbackReason.Should().BeEmpty();
        snap.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public void Report_RaisesUpdatedOncePerCall()
    {
        // Arrange
        var service = new InferenceDiagnosticsService();
        var info = new InferenceEngineInfo { Provider = InferenceProvider.Cpu };
        var fireCount = 0;
        service.Updated += (_, _) => fireCount++;

        // Act
        service.Report("Generation", info);
        service.Report("Embedding", info);

        // Assert — Updated fires once per Report.
        fireCount.Should().Be(2);
    }

    [Fact]
    public void Report_StoresPerEngineKindIndependently()
    {
        // Arrange
        var service = new InferenceDiagnosticsService();

        // Act — report distinct providers for each engine kind.
        service.Report("Embedding", new InferenceEngineInfo
        {
            Provider = InferenceProvider.DirectML,
        });
        service.Report("Generation", new InferenceEngineInfo
        {
            Provider = InferenceProvider.Cpu,
            IsFallback = true,
            FallbackReason = InferenceFallbackReason.NativeLibraryLoadFailed,
        });

        // Assert — each getter returns its own snapshot independently.
        service.Embedding.EngineKind.Should().Be("Embedding");
        service.Embedding.Provider.Should().Be("DirectML");
        service.Embedding.IsFallback.Should().BeFalse();

        service.Generation.EngineKind.Should().Be("Generation");
        service.Generation.Provider.Should().Be("Cpu");
        service.Generation.IsFallback.Should().BeTrue();
    }

    [Fact]
    public void Embedding_Generation_NotLoadedUntilReported()
    {
        // Arrange
        var service = new InferenceDiagnosticsService();

        // Assert — before any Report, snapshots are non-null but not loaded.
        service.Embedding.Should().NotBeNull();
        service.Generation.Should().NotBeNull();
        service.Embedding.IsLoaded.Should().BeFalse();
        service.Generation.IsLoaded.Should().BeFalse();
        service.Embedding.EngineKind.Should().Be("Embedding");
        service.Generation.EngineKind.Should().Be("Generation");
    }
}
