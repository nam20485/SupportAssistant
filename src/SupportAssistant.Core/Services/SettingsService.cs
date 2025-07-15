using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SupportAssistant.Core.Models;

namespace SupportAssistant.Core.Services;

/// <summary>
/// Interface for application settings management
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Current application settings
    /// </summary>
    ApplicationSettings Settings { get; }
    
    /// <summary>
    /// Event raised when settings are changed
    /// </summary>
    event EventHandler<ApplicationSettings>? SettingsChanged;
    
    /// <summary>
    /// Load settings from storage
    /// </summary>
    Task LoadSettingsAsync();
    
    /// <summary>
    /// Save settings to storage
    /// </summary>
    Task SaveSettingsAsync();
    
    /// <summary>
    /// Update specific settings section
    /// </summary>
    Task UpdateSettingsAsync(ApplicationSettings newSettings);
    
    /// <summary>
    /// Reset settings to default values
    /// </summary>
    Task ResetToDefaultsAsync();
    
    /// <summary>
    /// Get the settings file path
    /// </summary>
    string GetSettingsFilePath();
    
    /// <summary>
    /// Validate settings and fix any invalid values
    /// </summary>
    void ValidateAndFixSettings();
}

/// <summary>
/// JSON file-based settings service implementation
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsDirectoryPath;
    private readonly string _settingsFilePath;
    private ApplicationSettings _settings;

    public ApplicationSettings Settings => _settings;
    
    public event EventHandler<ApplicationSettings>? SettingsChanged;

    public SettingsService()
    {
        // Use standard application data path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsDirectoryPath = Path.Combine(appDataPath, "SupportAssistant");
        _settingsFilePath = Path.Combine(_settingsDirectoryPath, "settings.json");
        
        _settings = new ApplicationSettings();
        
        // Ensure directory exists
        Directory.CreateDirectory(_settingsDirectoryPath);
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loadedSettings = JsonSerializer.Deserialize<ApplicationSettings>(json, GetJsonOptions());
                
                if (loadedSettings != null)
                {
                    _settings = loadedSettings;
                    ValidateAndFixSettings();
                }
            }
            else
            {
                // First run - create default settings
                _settings = CreateDefaultSettings();
                await SaveSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            // If loading fails, use defaults and log error
            Console.WriteLine($"Failed to load settings: {ex.Message}. Using defaults.");
            _settings = CreateDefaultSettings();
            await SaveSettingsAsync();
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            ValidateAndFixSettings();
            
            var json = JsonSerializer.Serialize(_settings, GetJsonOptions());
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
            throw;
        }
    }

    public async Task UpdateSettingsAsync(ApplicationSettings newSettings)
    {
        _settings = newSettings;
        ValidateAndFixSettings();
        await SaveSettingsAsync();
        
        SettingsChanged?.Invoke(this, _settings);
    }

    public async Task ResetToDefaultsAsync()
    {
        _settings = CreateDefaultSettings();
        await SaveSettingsAsync();
        
        SettingsChanged?.Invoke(this, _settings);
    }

    public string GetSettingsFilePath() => _settingsFilePath;

    public void ValidateAndFixSettings()
    {
        // Validate AI settings
        _settings.Ai.Temperature = Math.Clamp(_settings.Ai.Temperature, 0.0, 1.0);
        _settings.Ai.TopP = Math.Clamp(_settings.Ai.TopP, 0.0, 1.0);
        _settings.Ai.TopK = Math.Max(1, _settings.Ai.TopK);
        _settings.Ai.MaxTokens = Math.Clamp(_settings.Ai.MaxTokens, 1, 4096);
        _settings.Ai.MaxContextLength = Math.Clamp(_settings.Ai.MaxContextLength, 512, 8192);
        _settings.Ai.InferenceTimeoutSeconds = Math.Clamp(_settings.Ai.InferenceTimeoutSeconds, 5, 300);

        // Validate knowledge base settings
        _settings.KnowledgeBase.ChunkSize = Math.Clamp(_settings.KnowledgeBase.ChunkSize, 100, 5000);
        _settings.KnowledgeBase.ChunkOverlap = Math.Clamp(_settings.KnowledgeBase.ChunkOverlap, 0, _settings.KnowledgeBase.ChunkSize / 2);
        _settings.KnowledgeBase.TopK = Math.Clamp(_settings.KnowledgeBase.TopK, 1, 20);
        _settings.KnowledgeBase.SimilarityThreshold = Math.Clamp(_settings.KnowledgeBase.SimilarityThreshold, 0.0, 1.0);
        _settings.KnowledgeBase.UpdateIntervalHours = Math.Clamp(_settings.KnowledgeBase.UpdateIntervalHours, 1, 168); // 1 hour to 1 week

        // Validate UI settings
        _settings.Ui.WindowWidth = Math.Clamp(_settings.Ui.WindowWidth, 400, 2560);
        _settings.Ui.WindowHeight = Math.Clamp(_settings.Ui.WindowHeight, 300, 1440);
        _settings.Ui.FontSize = Math.Clamp(_settings.Ui.FontSize, 8, 32);

        // Validate performance settings
        _settings.Performance.CpuThreads = Math.Max(0, _settings.Performance.CpuThreads);
        _settings.Performance.MaxMemoryMB = Math.Max(0, _settings.Performance.MaxMemoryMB);
        _settings.Performance.MaxCacheSizeMB = Math.Clamp(_settings.Performance.MaxCacheSizeMB, 10, 1024);
        _settings.Performance.MaxLogFileSizeMB = Math.Clamp(_settings.Performance.MaxLogFileSizeMB, 1, 100);
        _settings.Performance.LogFileRetentionCount = Math.Clamp(_settings.Performance.LogFileRetentionCount, 1, 50);

        // Validate enum-like string values
        if (!IsValidTheme(_settings.General.Theme))
            _settings.General.Theme = "Auto";
            
        if (!IsValidExecutionProvider(_settings.Ai.ExecutionProvider))
            _settings.Ai.ExecutionProvider = "DirectML";
            
        if (!IsValidMessageDensity(_settings.Ui.MessageDensity))
            _settings.Ui.MessageDensity = "Normal";
            
        if (!IsValidLogLevel(_settings.Performance.LogLevel))
            _settings.Performance.LogLevel = "Information";

        // Ensure default model path if empty
        if (string.IsNullOrEmpty(_settings.Ai.ModelPath))
        {
            var defaultModelsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SupportAssistant", "Models", "phi-3-mini.onnx");
            _settings.Ai.ModelPath = defaultModelsPath;
        }

        // Ensure default knowledge base path if empty
        if (string.IsNullOrEmpty(_settings.KnowledgeBase.KnowledgeBasePath))
        {
            var defaultKbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SupportAssistant", "KnowledgeBase");
            _settings.KnowledgeBase.KnowledgeBasePath = defaultKbPath;
        }
    }

    private ApplicationSettings CreateDefaultSettings()
    {
        var settings = new ApplicationSettings();
        
        // Set default paths
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var supportAssistantPath = Path.Combine(appDataPath, "SupportAssistant");
        
        settings.Ai.ModelPath = Path.Combine(supportAssistantPath, "Models", "phi-3-mini.onnx");
        settings.KnowledgeBase.KnowledgeBasePath = Path.Combine(supportAssistantPath, "KnowledgeBase");
        
        return settings;
    }

    private JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    private static bool IsValidTheme(string theme)
    {
        return theme is "Light" or "Dark" or "Auto";
    }

    private static bool IsValidExecutionProvider(string provider)
    {
        return provider is "DirectML" or "CPU" or "CUDA" or "ROCm";
    }

    private static bool IsValidMessageDensity(string density)
    {
        return density is "Compact" or "Normal" or "Comfortable";
    }

    private static bool IsValidLogLevel(string logLevel)
    {
        return logLevel is "Debug" or "Information" or "Warning" or "Error";
    }
}
