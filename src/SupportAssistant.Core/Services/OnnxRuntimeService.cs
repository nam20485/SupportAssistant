using Microsoft.ML.OnnxRuntime;
using System;
using System.Linq;

namespace SupportAssistant.Core.Services
{
    /// <summary>
    /// Implementation of ONNX Runtime service with DirectML support
    /// </summary>
    public class OnnxRuntimeService : IOnnxRuntimeService
    {
        private bool _isInitialized = false;
        private bool _isDirectMLAvailable = false;
        private string[] _availableProviders = Array.Empty<string>();

        /// <inheritdoc />
        public bool Initialize()
        {
            try
            {
                // Get available execution providers
                _availableProviders = OrtEnv.Instance().GetAvailableProviders();
                
                // Check if DirectML is available
                _isDirectMLAvailable = _availableProviders.Contains("DmlExecutionProvider");
                
                _isInitialized = true;
                
                return _isDirectMLAvailable;
            }
            catch (Exception)
            {
                // If initialization fails, we'll fall back to CPU only
                _availableProviders = new[] { "CPUExecutionProvider" };
                _isDirectMLAvailable = false;
                _isInitialized = true;
                
                return false;
            }
        }

        /// <inheritdoc />
        public string[] GetAvailableProviders()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            
            return _availableProviders;
        }

        /// <inheritdoc />
        public bool IsDirectMLAvailable()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            
            return _isDirectMLAvailable;
        }

        /// <inheritdoc />
        public SessionOptions CreateSessionOptions()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            var sessionOptions = new SessionOptions();
            
            try
            {
                if (_isDirectMLAvailable)
                {
                    // Add DirectML execution provider for GPU acceleration
                    sessionOptions.AppendExecutionProvider_DML(0);
                }
                
                // CPU is always available as fallback
                sessionOptions.AppendExecutionProvider_CPU();
                
                // Set other performance options
                sessionOptions.EnableCpuMemArena = true;
                sessionOptions.EnableMemoryPattern = true;
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                return sessionOptions;
            }
            catch (Exception)
            {
                // If DirectML fails, create CPU-only session options
                sessionOptions = new SessionOptions();
                sessionOptions.AppendExecutionProvider_CPU();
                sessionOptions.EnableCpuMemArena = true;
                sessionOptions.EnableMemoryPattern = true;
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                return sessionOptions;
            }
        }
    }
}
