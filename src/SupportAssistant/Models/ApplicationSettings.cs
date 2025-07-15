using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SupportAssistant.Models;

/// <summary>
/// Application settings model for configuration persistence
/// </summary>
public class ApplicationSettings
{
    /// <summary>
    /// General application settings
    /// </summary>
    public GeneralSettings General { get; set; } = new();
    
    /// <summary>
    /// AI model and processing settings
    /// </summary>
    public AiSettings Ai { get; set; } = new();
    
    /// <summary>
    /// Knowledge base configuration
    /// </summary>
    public KnowledgeBaseSettings KnowledgeBase { get; set; } = new();
    
    /// <summary>
    /// User interface preferences
    /// </summary>
    public UiSettings Ui { get; set; } = new();
    
    /// <summary>
    /// Performance and system settings
    /// </summary>
    public PerformanceSettings Performance { get; set; } = new();
}

/// <summary>
/// General application settings
/// </summary>
public class GeneralSettings
{
    /// <summary>
    /// Application theme (Light, Dark, Auto)
    /// </summary>
    public string Theme { get; set; } = "Auto";
    
    /// <summary>
    /// Language/locale setting
    /// </summary>
    public string Language { get; set; } = "en-US";
    
    /// <summary>
    /// Whether to start application on system startup
    /// </summary>
    public bool StartWithSystem { get; set; } = false;
    
    /// <summary>
    /// Whether to minimize to system tray
    /// </summary>
    public bool MinimizeToTray { get; set; } = false;
    
    /// <summary>
    /// Whether to enable automatic updates
    /// </summary>
    public bool EnableAutoUpdate { get; set; } = true;
    
    /// <summary>
    /// First run flag
    /// </summary>
    public bool IsFirstRun { get; set; } = true;
    
    /// <summary>
    /// Last application version
    /// </summary>
    public string LastVersion { get; set; } = string.Empty;
}

/// <summary>
/// AI model and processing settings
/// </summary>
public class AiSettings
{
    /// <summary>
    /// Path to the ONNX model file
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;
    
    /// <summary>
    /// ONNX execution provider (DirectML, CPU, etc.)
    /// </summary>
    public string ExecutionProvider { get; set; } = "DirectML";
    
    /// <summary>
    /// Maximum context length for prompts
    /// </summary>
    public int MaxContextLength { get; set; } = 2048;
    
    /// <summary>
    /// Temperature for text generation (0.0 - 1.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;
    
    /// <summary>
    /// Top-p value for nucleus sampling
    /// </summary>
    public double TopP { get; set; } = 0.9;
    
    /// <summary>
    /// Top-k value for sampling
    /// </summary>
    public int TopK { get; set; } = 40;
    
    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 512;
    
    /// <summary>
    /// Timeout for AI inference (seconds)
    /// </summary>
    public int InferenceTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Whether to enable response streaming
    /// </summary>
    public bool EnableStreaming { get; set; } = true;
}

/// <summary>
/// Knowledge base configuration settings
/// </summary>
public class KnowledgeBaseSettings
{
    /// <summary>
    /// Path to knowledge base directory
    /// </summary>
    public string KnowledgeBasePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to auto-update knowledge base
    /// </summary>
    public bool AutoUpdate { get; set; } = true;
    
    /// <summary>
    /// Knowledge base update interval (hours)
    /// </summary>
    public int UpdateIntervalHours { get; set; } = 24;
    
    /// <summary>
    /// Maximum chunk size for text processing
    /// </summary>
    public int ChunkSize { get; set; } = 1000;
    
    /// <summary>
    /// Chunk overlap for better context preservation
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;
    
    /// <summary>
    /// Number of top results to retrieve
    /// </summary>
    public int TopK { get; set; } = 5;
    
    /// <summary>
    /// Minimum similarity threshold (0.0 - 1.0)
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.7;
    
    /// <summary>
    /// Supported file extensions for knowledge base
    /// </summary>
    public List<string> SupportedExtensions { get; set; } = new() { ".md", ".txt", ".pdf" };
}

/// <summary>
/// User interface preferences
/// </summary>
public class UiSettings
{
    /// <summary>
    /// Main window width
    /// </summary>
    public double WindowWidth { get; set; } = 800;
    
    /// <summary>
    /// Main window height
    /// </summary>
    public double WindowHeight { get; set; } = 600;
    
    /// <summary>
    /// Window position X
    /// </summary>
    public double WindowX { get; set; } = double.NaN;
    
    /// <summary>
    /// Window position Y
    /// </summary>
    public double WindowY { get; set; } = double.NaN;
    
    /// <summary>
    /// Whether window is maximized
    /// </summary>
    public bool IsMaximized { get; set; } = false;
    
    /// <summary>
    /// Chat font size
    /// </summary>
    public double FontSize { get; set; } = 14;
    
    /// <summary>
    /// Chat font family
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";
    
    /// <summary>
    /// Whether to show timestamps on messages
    /// </summary>
    public bool ShowTimestamps { get; set; } = true;
    
    /// <summary>
    /// Whether to show typing indicators
    /// </summary>
    public bool ShowTypingIndicators { get; set; } = true;
    
    /// <summary>
    /// Whether to enable auto-scroll
    /// </summary>
    public bool EnableAutoScroll { get; set; } = true;
    
    /// <summary>
    /// Whether to enable sound notifications
    /// </summary>
    public bool EnableSounds { get; set; } = true;
    
    /// <summary>
    /// Message density (Compact, Normal, Comfortable)
    /// </summary>
    public string MessageDensity { get; set; } = "Normal";
}

/// <summary>
/// Performance and system settings
/// </summary>
public class PerformanceSettings
{
    /// <summary>
    /// Number of CPU threads to use (0 = auto)
    /// </summary>
    public int CpuThreads { get; set; } = 0;
    
    /// <summary>
    /// Maximum memory usage in MB (0 = unlimited)
    /// </summary>
    public int MaxMemoryMB { get; set; } = 0;
    
    /// <summary>
    /// Enable GPU acceleration when available
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;
    
    /// <summary>
    /// Cache embeddings for faster retrieval
    /// </summary>
    public bool CacheEmbeddings { get; set; } = true;
    
    /// <summary>
    /// Maximum cache size in MB
    /// </summary>
    public int MaxCacheSizeMB { get; set; } = 100;
    
    /// <summary>
    /// Enable performance monitoring
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = false;
    
    /// <summary>
    /// Log level (Debug, Information, Warning, Error)
    /// </summary>
    public string LogLevel { get; set; } = "Information";
    
    /// <summary>
    /// Maximum log file size in MB
    /// </summary>
    public int MaxLogFileSizeMB { get; set; } = 10;
    
    /// <summary>
    /// Number of log files to retain
    /// </summary>
    public int LogFileRetentionCount { get; set; } = 5;
}
