using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SupportAssistant.Core.Engines;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sa-settings-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new SettingsService(_tempDir);
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
            // Best-effort cleanup for temp test dirs.
        }
    }

    [Fact]
    public async Task LoadSettingsAsync_AppliesPersistedExecutionProvider()
    {
        // Arrange — persist via the same serializer the app uses, then reload into a fresh service.
        _sut.Settings.General.IsFirstRun = false;
        _sut.Settings.Ai.ExecutionProvider = "CPU";
        await _sut.SaveSettingsAsync();

        var reloaded = new SettingsService(_tempDir);

        // Act — mirrors App.axaml.cs calling LoadSettingsAsync before consumers read settings.
        await reloaded.LoadSettingsAsync();

        // Assert
        reloaded.Settings.Ai.ExecutionProvider.Should().Be("CPU");
        reloaded.Settings.General.IsFirstRun.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSettingsAsync_BeforeLoad_DefaultsAreNotPersistedValues()
    {
        _sut.Settings.Ai.ExecutionProvider = "ROCm";
        _sut.Settings.General.IsFirstRun = false;
        await _sut.SaveSettingsAsync();

        var reloaded = new SettingsService(_tempDir);

        // Before load, in-memory defaults must not silently pretend to be the file contents.
        reloaded.Settings.Ai.ExecutionProvider.Should().Be("DirectML");
        reloaded.Settings.General.IsFirstRun.Should().BeTrue();

        await reloaded.LoadSettingsAsync();

        reloaded.Settings.Ai.ExecutionProvider.Should().Be("ROCm");
        reloaded.Settings.General.IsFirstRun.Should().BeFalse();
    }

    [Fact]
    public void InferenceOptionsFactory_HonorsLoadedCpuExecutionProvider()
    {
        _sut.Settings.Ai.ExecutionProvider = "CPU";

        var options = InferenceOptionsFactory.Create(_sut);

        options.UseGpuAcceleration.Should().BeFalse();
    }
}
