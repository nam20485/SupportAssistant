using Microsoft.ML.OnnxRuntime;
using System;

namespace SupportAssistant.Core.Services
{
    /// <summary>
    /// Service for initializing and managing ONNX Runtime with DirectML acceleration
    /// </summary>
    public interface IOnnxRuntimeService
    {
        /// <summary>
        /// Initializes ONNX Runtime and verifies DirectML provider availability
        /// </summary>
        /// <returns>True if DirectML is available, false if falling back to CPU</returns>
        bool Initialize();
        
        /// <summary>
        /// Gets the available execution providers
        /// </summary>
        /// <returns>Array of available execution provider names</returns>
        string[] GetAvailableProviders();
        
        /// <summary>
        /// Checks if DirectML provider is available
        /// </summary>
        /// <returns>True if DirectML is available</returns>
        bool IsDirectMLAvailable();
        
        /// <summary>
        /// Creates session options with the best available execution provider
        /// </summary>
        /// <returns>Configured SessionOptions</returns>
        SessionOptions CreateSessionOptions();
    }
}
