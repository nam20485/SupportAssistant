using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using InferenceEngine.Core.Text;
using ReactiveUI;
using SupportAssistant.Core.Engines;
using SupportAssistant.Core.Models;
using SupportAssistant.Core.Services;

namespace SupportAssistant.ViewModels;

/// <summary>
/// ViewModel for the Settings view
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IInferenceDiagnosticsService _diag;
    private readonly TextEmbeddingEngine _embeddingEngine;
    private readonly TextGenerationEngine _generationEngine;
    private ApplicationSettings _settings;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        IInferenceDiagnosticsService diagnostics,
        TextEmbeddingEngine embeddingEngine,
        TextGenerationEngine generationEngine)
    {
        _settingsService = settingsService;
        _diag = diagnostics;
        _embeddingEngine = embeddingEngine;
        _generationEngine = generationEngine;
        _settings = _settingsService.Settings;

        // Refresh the acceleration-status block whenever either engine reports a load/fallback.
        // The callback fires on a thread-pool thread, so marshal to the UI thread.
        _diag.Updated += OnDiagUpdated;
        RefreshDiagnostics();

        // Commands
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        ResetCommand = ReactiveCommand.CreateFromTask(ResetAsync);
        CancelCommand = ReactiveCommand.Create(Cancel);
        BrowseModelPathCommand = ReactiveCommand.Create(BrowseModelPath);
        BrowseKnowledgeBasePathCommand = ReactiveCommand.Create(BrowseKnowledgeBasePath);
        ProbeAccelerationCommand = ReactiveCommand.CreateFromTask(ProbeAccelerationAsync);
        
        // Load current settings into editable properties
        LoadSettingsIntoProperties();
    }

    #region Commands
    
    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseModelPathCommand { get; }
    public ICommand BrowseKnowledgeBasePathCommand { get; }
    public ReactiveCommand<Unit, Unit> ProbeAccelerationCommand { get; }
    
    #endregion

    /// <summary>
    /// Loads both inference engines so <c>OnSessionInitialized</c> diagnostics populate. Safe to
    /// call repeatedly and from Settings-open: the engines are CPU-forced in-process by default on
    /// Linux (<see cref="InferenceOptionsFactory.Create"/>'s <c>allowGpuInProcess</c>), so this can
    /// no longer trigger the Mesa/MIGraphX LLVM collision. See docs/plans/
    /// inference-engine-integration-status.md §C.
    /// </summary>
    public Task EnsureAccelerationProbedAsync() => ProbeAccelerationAsync();

    #region Properties

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    // Acceleration Status (WS1): read-only, UI-thread-marshaled properties refreshed from the
    // inference diagnostics service. Updated by RefreshDiagnostics / ProbeAccelerationAsync.
    private string _embeddingStatus = "Embedding: not probed yet";
    public string EmbeddingStatus
    {
        get => _embeddingStatus;
        set => this.RaiseAndSetIfChanged(ref _embeddingStatus, value);
    }

    private string _generationStatus = "Generation: not probed yet";
    public string GenerationStatus
    {
        get => _generationStatus;
        set => this.RaiseAndSetIfChanged(ref _generationStatus, value);
    }

    private string _fallbackNotice = string.Empty;
    public string FallbackNotice
    {
        get => _fallbackNotice;
        set => this.RaiseAndSetIfChanged(ref _fallbackNotice, value);
    }

    private bool _hasFallback;
    public bool HasFallback
    {
        get => _hasFallback;
        set => this.RaiseAndSetIfChanged(ref _hasFallback, value);
    }

    private string _hostPreflightSummary = string.Empty;
    public string HostPreflightSummary
    {
        get => _hostPreflightSummary;
        set => this.RaiseAndSetIfChanged(ref _hostPreflightSummary, value);
    }

    private string _diagnosticsDetails = string.Empty;
    public string DiagnosticsDetails
    {
        get => _diagnosticsDetails;
        set => this.RaiseAndSetIfChanged(ref _diagnosticsDetails, value);
    }

    private bool _isProbingAcceleration;

    /// <summary>Bound to the Refresh button's IsEnabled.</summary>
    public bool IsProbingAcceleration
    {
        get => _isProbingAcceleration;
        set => this.RaiseAndSetIfChanged(ref _isProbingAcceleration, value);
    }

    private void OnDiagUpdated(object? sender, EventArgs e) =>
        RxApp.MainThreadScheduler.Schedule(RefreshDiagnostics);

    private void RefreshDiagnostics()
    {
        var em = _diag.Embedding;
        var ge = _diag.Generation;

        EmbeddingStatus = em.IsLoaded
            ? FormatEngineStatus(em)
            : IsProbingAcceleration
                ? "Embedding: probing… (MIGraphX compile can take ~30s)"
                : "Embedding: not probed yet";
        GenerationStatus = ge.IsLoaded
            ? FormatEngineStatus(ge)
            : IsProbingAcceleration
                ? "Generation: probing… (first model load can take minutes)"
                : "Generation: not probed yet";

        var fb = new[] { em, ge }
            .Where(x => x.IsFallback)
            .Select(x => $"{x.EngineKind} fell back to CPU — {x.FallbackReason}")
            .ToList();
        FallbackNotice = string.Join("\n", fb);
        HasFallback = fb.Any();

        HostPreflightSummary = BuildHostPreflightSummary();
        DiagnosticsDetails = BuildDiagnosticsDetails(em, ge);
    }

    private static string FormatEngineStatus(EngineDiagnostics diag)
    {
        var suffix = diag.IsFallback ? " (CPU fallback)" : string.Empty;
        return $"{diag.EngineKind}: {diag.Provider}{suffix}";
    }

    private string BuildHostPreflightSummary()
    {
        var provider = ExecutionProvider.Trim();
        var builder = new StringBuilder();
        builder.Append($"Configured provider: {provider}");

        if (OperatingSystem.IsLinux())
        {
            var hsa = Environment.GetEnvironmentVariable(RocmHostPreflight.OverrideEnvironmentVariable);
            builder.Append($"\n{RocmHostPreflight.OverrideEnvironmentVariable}=");
            builder.Append(string.IsNullOrEmpty(hsa) ? "(unset)" : hsa);

            var preflight = InferenceOptionsFactory.LastRocmPreflight ?? RocmHostPreflight.Evaluate();
            builder.Append($"\nROCm host preflight: {preflight.Status}");
            if (!string.IsNullOrWhiteSpace(preflight.Message))
            {
                builder.Append($"\n{preflight.Message}");
            }

            // WS5: interim safety — InferenceOptionsFactory.Create forces CPU for the real,
            // already-constructed engines whenever this is true (see App.axaml.cs) — avoids the
            // Mesa/MIGraphX LLVM CommandLine collision. Reflects the actual decision already made,
            // not a guess, so this note only appears when it's actually in effect.
            if (InferenceOptionsFactory.LastForcedCpuForGuiSafety)
            {
                builder.Append(
                    "\nNote: GPU acceleration is CPU-forced in this process (WS5 interim safety — " +
                    "avoids a Mesa/MIGraphX LLVM crash; see " +
                    "docs/plans/inference-engine-integration-status.md §C). Verify the real GPU/ROCm " +
                    "path safely with:\n" +
                    "    dotnet test --filter FullyQualifiedName~RocmExecutionProviderSmokeTests -v n");
            }
        }

        return builder.ToString();
    }

    private static string BuildDiagnosticsDetails(EngineDiagnostics embedding, EngineDiagnostics generation)
    {
        var lines = new List<string>();
        AppendCheckLines(lines, embedding);
        AppendCheckLines(lines, generation);
        return lines.Count == 0
            ? "EP selection trail appears after engines load (Refresh acceleration status)."
            : string.Join('\n', lines);
    }

    private static void AppendCheckLines(List<string> lines, EngineDiagnostics diag)
    {
        if (!diag.IsLoaded || diag.Checks.Count == 0)
        {
            return;
        }

        lines.Add($"--- {diag.EngineKind} ---");
        lines.AddRange(diag.Checks);
    }

    private async Task ProbeAccelerationAsync()
    {
        if (IsProbingAcceleration)
        {
            return;
        }

        // Safe: the already-constructed engines (App.axaml.cs) are CPU-forced by default on Linux
        // (InferenceOptionsFactory.Create's allowGpuInProcess = false), so LoadAsync() here can no
        // longer trigger the Mesa/MIGraphX LLVM collision. See docs/plans/
        // inference-engine-integration-status.md §C.
        IsProbingAcceleration = true;
        RefreshDiagnostics();
        try
        {
            if (!_diag.Embedding.IsLoaded)
            {
                EmbeddingStatus = "Embedding: probing… (MIGraphX compile can take ~30s)";
                await Task.Run(async () => await _embeddingEngine.LoadAsync().ConfigureAwait(false)).ConfigureAwait(true);
            }

            RefreshDiagnostics();

            if (!_diag.Generation.IsLoaded)
            {
                GenerationStatus = "Generation: probing… (first model load can take minutes)";
                await Task.Run(async () => await _generationEngine.LoadAsync().ConfigureAwait(false)).ConfigureAwait(true);
            }

            RefreshDiagnostics();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Acceleration probe failed: {ex.Message}";
            RefreshDiagnostics();
        }
        finally
        {
            IsProbingAcceleration = false;
            RefreshDiagnostics();
        }
    }

    // General Settings
    private string _theme = "Auto";
    public string Theme
    {
        get => _theme;
        set => this.RaiseAndSetIfChanged(ref _theme, value);
    }

    private string _language = "en-US";
    public string Language
    {
        get => _language;
        set => this.RaiseAndSetIfChanged(ref _language, value);
    }

    private bool _startWithSystem = false;
    public bool StartWithSystem
    {
        get => _startWithSystem;
        set => this.RaiseAndSetIfChanged(ref _startWithSystem, value);
    }

    private bool _minimizeToTray = false;
    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => this.RaiseAndSetIfChanged(ref _minimizeToTray, value);
    }

    private bool _enableAutoUpdate = true;
    public bool EnableAutoUpdate
    {
        get => _enableAutoUpdate;
        set => this.RaiseAndSetIfChanged(ref _enableAutoUpdate, value);
    }

    // AI Settings
    private string _modelPath = string.Empty;
    public string ModelPath
    {
        get => _modelPath;
        set => this.RaiseAndSetIfChanged(ref _modelPath, value);
    }

    private string _executionProvider = "DirectML";
    public string ExecutionProvider
    {
        get => _executionProvider;
        set => this.RaiseAndSetIfChanged(ref _executionProvider, value);
    }

    private int _maxContextLength = 2048;
    public int MaxContextLength
    {
        get => _maxContextLength;
        set => this.RaiseAndSetIfChanged(ref _maxContextLength, value);
    }

    private double _temperature = 0.7;
    public double Temperature
    {
        get => _temperature;
        set => this.RaiseAndSetIfChanged(ref _temperature, value);
    }

    private double _topP = 0.9;
    public double TopP
    {
        get => _topP;
        set => this.RaiseAndSetIfChanged(ref _topP, value);
    }

    private int _topK = 40;
    public int TopK
    {
        get => _topK;
        set => this.RaiseAndSetIfChanged(ref _topK, value);
    }

    private int _maxTokens = 512;
    public int MaxTokens
    {
        get => _maxTokens;
        set => this.RaiseAndSetIfChanged(ref _maxTokens, value);
    }

    private int _inferenceTimeoutSeconds = 30;
    public int InferenceTimeoutSeconds
    {
        get => _inferenceTimeoutSeconds;
        set => this.RaiseAndSetIfChanged(ref _inferenceTimeoutSeconds, value);
    }

    private bool _enableStreaming = true;
    public bool EnableStreaming
    {
        get => _enableStreaming;
        set => this.RaiseAndSetIfChanged(ref _enableStreaming, value);
    }

    private bool _bypassLegacyRocmGpuDialog;
    public bool BypassLegacyRocmGpuDialog
    {
        get => _bypassLegacyRocmGpuDialog;
        set => this.RaiseAndSetIfChanged(ref _bypassLegacyRocmGpuDialog, value);
    }

    // Knowledge Base Settings
    private string _knowledgeBasePath = string.Empty;
    public string KnowledgeBasePath
    {
        get => _knowledgeBasePath;
        set => this.RaiseAndSetIfChanged(ref _knowledgeBasePath, value);
    }

    private bool _autoUpdateKnowledgeBase = true;
    public bool AutoUpdateKnowledgeBase
    {
        get => _autoUpdateKnowledgeBase;
        set => this.RaiseAndSetIfChanged(ref _autoUpdateKnowledgeBase, value);
    }

    private int _updateIntervalHours = 24;
    public int UpdateIntervalHours
    {
        get => _updateIntervalHours;
        set => this.RaiseAndSetIfChanged(ref _updateIntervalHours, value);
    }

    private int _chunkSize = 1000;
    public int ChunkSize
    {
        get => _chunkSize;
        set => this.RaiseAndSetIfChanged(ref _chunkSize, value);
    }

    private int _chunkOverlap = 200;
    public int ChunkOverlap
    {
        get => _chunkOverlap;
        set => this.RaiseAndSetIfChanged(ref _chunkOverlap, value);
    }

    private int _retrievalTopK = 5;
    public int RetrievalTopK
    {
        get => _retrievalTopK;
        set => this.RaiseAndSetIfChanged(ref _retrievalTopK, value);
    }

    private double _similarityThreshold = 0.7;
    public double SimilarityThreshold
    {
        get => _similarityThreshold;
        set => this.RaiseAndSetIfChanged(ref _similarityThreshold, value);
    }

    // UI Settings
    private double _fontSize = 14;
    public double FontSize
    {
        get => _fontSize;
        set => this.RaiseAndSetIfChanged(ref _fontSize, value);
    }

    private string _fontFamily = "Segoe UI";
    public string FontFamily
    {
        get => _fontFamily;
        set => this.RaiseAndSetIfChanged(ref _fontFamily, value);
    }

    private bool _showTimestamps = true;
    public bool ShowTimestamps
    {
        get => _showTimestamps;
        set => this.RaiseAndSetIfChanged(ref _showTimestamps, value);
    }

    private bool _showTypingIndicators = true;
    public bool ShowTypingIndicators
    {
        get => _showTypingIndicators;
        set => this.RaiseAndSetIfChanged(ref _showTypingIndicators, value);
    }

    private bool _enableAutoScroll = true;
    public bool EnableAutoScroll
    {
        get => _enableAutoScroll;
        set => this.RaiseAndSetIfChanged(ref _enableAutoScroll, value);
    }

    private bool _enableSounds = true;
    public bool EnableSounds
    {
        get => _enableSounds;
        set => this.RaiseAndSetIfChanged(ref _enableSounds, value);
    }

    private string _messageDensity = "Normal";
    public string MessageDensity
    {
        get => _messageDensity;
        set => this.RaiseAndSetIfChanged(ref _messageDensity, value);
    }

    // Performance Settings
    private int _cpuThreads = 0;
    public int CpuThreads
    {
        get => _cpuThreads;
        set => this.RaiseAndSetIfChanged(ref _cpuThreads, value);
    }

    private int _maxMemoryMB = 0;
    public int MaxMemoryMB
    {
        get => _maxMemoryMB;
        set => this.RaiseAndSetIfChanged(ref _maxMemoryMB, value);
    }

    private bool _enableGpuAcceleration = true;
    public bool EnableGpuAcceleration
    {
        get => _enableGpuAcceleration;
        set => this.RaiseAndSetIfChanged(ref _enableGpuAcceleration, value);
    }

    private bool _cacheEmbeddings = true;
    public bool CacheEmbeddings
    {
        get => _cacheEmbeddings;
        set => this.RaiseAndSetIfChanged(ref _cacheEmbeddings, value);
    }

    private int _maxCacheSizeMB = 100;
    public int MaxCacheSizeMB
    {
        get => _maxCacheSizeMB;
        set => this.RaiseAndSetIfChanged(ref _maxCacheSizeMB, value);
    }

    private string _logLevel = "Information";
    public string LogLevel
    {
        get => _logLevel;
        set => this.RaiseAndSetIfChanged(ref _logLevel, value);
    }

    #endregion

    #region Available Options

    public List<string> AvailableThemes { get; } = new() { "Light", "Dark", "Auto" };
    public List<string> AvailableExecutionProviders { get; } = new() { "DirectML", "CPU", "CUDA", "ROCm" };
    public List<string> AvailableMessageDensities { get; } = new() { "Compact", "Normal", "Comfortable" };
    public List<string> AvailableLogLevels { get; } = new() { "Debug", "Information", "Warning", "Error" };

    #endregion

    #region Methods

    private void LoadSettingsIntoProperties()
    {
        // General
        Theme = _settings.General.Theme;
        Language = _settings.General.Language;
        StartWithSystem = _settings.General.StartWithSystem;
        MinimizeToTray = _settings.General.MinimizeToTray;
        EnableAutoUpdate = _settings.General.EnableAutoUpdate;

        // AI
        ModelPath = _settings.Ai.ModelPath;
        ExecutionProvider = _settings.Ai.ExecutionProvider;
        MaxContextLength = _settings.Ai.MaxContextLength;
        Temperature = _settings.Ai.Temperature;
        TopP = _settings.Ai.TopP;
        TopK = _settings.Ai.TopK;
        MaxTokens = _settings.Ai.MaxTokens;
        InferenceTimeoutSeconds = _settings.Ai.InferenceTimeoutSeconds;
        EnableStreaming = _settings.Ai.EnableStreaming;
        BypassLegacyRocmGpuDialog = _settings.Ai.BypassLegacyRocmGpuDialog;

        // Knowledge Base
        KnowledgeBasePath = _settings.KnowledgeBase.KnowledgeBasePath;
        AutoUpdateKnowledgeBase = _settings.KnowledgeBase.AutoUpdate;
        UpdateIntervalHours = _settings.KnowledgeBase.UpdateIntervalHours;
        ChunkSize = _settings.KnowledgeBase.ChunkSize;
        ChunkOverlap = _settings.KnowledgeBase.ChunkOverlap;
        RetrievalTopK = _settings.KnowledgeBase.TopK;
        SimilarityThreshold = _settings.KnowledgeBase.SimilarityThreshold;

        // UI
        FontSize = _settings.Ui.FontSize;
        FontFamily = _settings.Ui.FontFamily;
        ShowTimestamps = _settings.Ui.ShowTimestamps;
        ShowTypingIndicators = _settings.Ui.ShowTypingIndicators;
        EnableAutoScroll = _settings.Ui.EnableAutoScroll;
        EnableSounds = _settings.Ui.EnableSounds;
        MessageDensity = _settings.Ui.MessageDensity;

        // Performance
        CpuThreads = _settings.Performance.CpuThreads;
        MaxMemoryMB = _settings.Performance.MaxMemoryMB;
        EnableGpuAcceleration = _settings.Performance.EnableGpuAcceleration;
        CacheEmbeddings = _settings.Performance.CacheEmbeddings;
        MaxCacheSizeMB = _settings.Performance.MaxCacheSizeMB;
        LogLevel = _settings.Performance.LogLevel;
    }

    private ApplicationSettings CreateSettingsFromProperties()
    {
        return new ApplicationSettings
        {
            General = new GeneralSettings
            {
                Theme = Theme,
                Language = Language,
                StartWithSystem = StartWithSystem,
                MinimizeToTray = MinimizeToTray,
                EnableAutoUpdate = EnableAutoUpdate,
                IsFirstRun = _settings.General.IsFirstRun, // Preserve existing value
                LastVersion = _settings.General.LastVersion // Preserve existing value
            },
            Ai = new AiSettings
            {
                ModelPath = ModelPath,
                ExecutionProvider = ExecutionProvider,
                MaxContextLength = MaxContextLength,
                Temperature = Temperature,
                TopP = TopP,
                TopK = TopK,
                MaxTokens = MaxTokens,
                InferenceTimeoutSeconds = InferenceTimeoutSeconds,
                EnableStreaming = EnableStreaming,
                BypassLegacyRocmGpuDialog = BypassLegacyRocmGpuDialog,
            },
            KnowledgeBase = new KnowledgeBaseSettings
            {
                KnowledgeBasePath = KnowledgeBasePath,
                AutoUpdate = AutoUpdateKnowledgeBase,
                UpdateIntervalHours = UpdateIntervalHours,
                ChunkSize = ChunkSize,
                ChunkOverlap = ChunkOverlap,
                TopK = RetrievalTopK,
                SimilarityThreshold = SimilarityThreshold,
                SupportedExtensions = _settings.KnowledgeBase.SupportedExtensions // Preserve existing value
            },
            Ui = new UiSettings
            {
                WindowWidth = _settings.Ui.WindowWidth, // Preserve window state
                WindowHeight = _settings.Ui.WindowHeight,
                WindowX = _settings.Ui.WindowX,
                WindowY = _settings.Ui.WindowY,
                IsMaximized = _settings.Ui.IsMaximized,
                FontSize = FontSize,
                FontFamily = FontFamily,
                ShowTimestamps = ShowTimestamps,
                ShowTypingIndicators = ShowTypingIndicators,
                EnableAutoScroll = EnableAutoScroll,
                EnableSounds = EnableSounds,
                MessageDensity = MessageDensity
            },
            Performance = new PerformanceSettings
            {
                CpuThreads = CpuThreads,
                MaxMemoryMB = MaxMemoryMB,
                EnableGpuAcceleration = EnableGpuAcceleration,
                CacheEmbeddings = CacheEmbeddings,
                MaxCacheSizeMB = MaxCacheSizeMB,
                EnablePerformanceMonitoring = _settings.Performance.EnablePerformanceMonitoring, // Preserve
                LogLevel = LogLevel,
                MaxLogFileSizeMB = _settings.Performance.MaxLogFileSizeMB, // Preserve
                LogFileRetentionCount = _settings.Performance.LogFileRetentionCount // Preserve
            }
        };
    }

    private async Task SaveAsync()
    {
        try
        {
            StatusMessage = "Saving settings...";

            var previousProvider = _settings.Ai.ExecutionProvider;
            var newSettings = CreateSettingsFromProperties();
            await _settingsService.UpdateSettingsAsync(newSettings);
            _settings = _settingsService.Settings;

            var providerChanged = !string.Equals(
                previousProvider,
                newSettings.Ai.ExecutionProvider,
                StringComparison.OrdinalIgnoreCase);

            // Probing here reflects the *current session's* already-constructed engines, which were
            // built from settings at app startup — not the just-saved value. A provider change only
            // takes effect after restart (App.axaml.cs constructs the engines once, at DI setup).
            await ProbeAccelerationAsync().ConfigureAwait(true);

            if (!StatusMessage.StartsWith("Acceleration probe failed", StringComparison.Ordinal))
            {
                StatusMessage = providerChanged
                    ? "Settings saved. Restart the app for the new execution provider to take effect."
                    : "Settings saved successfully!";
            }

            await Task.Delay(4000);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    private async Task ResetAsync()
    {
        try
        {
            StatusMessage = "Resetting to defaults...";
            
            await _settingsService.ResetToDefaultsAsync();
            _settings = _settingsService.Settings;
            LoadSettingsIntoProperties();
            
            StatusMessage = "Settings reset to defaults!";
            
            // Clear status message after a delay
            await Task.Delay(2000);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error resetting settings: {ex.Message}";
        }
    }

    private void Cancel()
    {
        // Reload current settings to discard changes
        _settings = _settingsService.Settings;
        LoadSettingsIntoProperties();
        StatusMessage = "Changes discarded";
    }

    private void BrowseModelPath()
    {
        // In a real implementation, this would open a file dialog
        // For now, just show a placeholder message
        StatusMessage = "Model path browsing not yet implemented";
    }

    private void BrowseKnowledgeBasePath()
    {
        // In a real implementation, this would open a folder dialog
        // For now, just show a placeholder message
        StatusMessage = "Knowledge base path browsing not yet implemented";
    }

    #endregion
}
