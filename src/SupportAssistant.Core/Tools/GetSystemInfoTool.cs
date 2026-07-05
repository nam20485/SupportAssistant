using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Tools
{
    /// <summary>
    /// Tool for collecting read-only system information (OS, runtime, memory, storage).
    /// </summary>
    public class GetSystemInfoTool : ITool
    {
        public string Name => "GetSystemInfo";

        public string Description => "Collect operating system, runtime, memory, and storage information";

        public ToolCategory Category => ToolCategory.System;

        public ToolPermissionLevel RequiredPermission => ToolPermissionLevel.Read;

        public bool RequiresApproval => false;

        public bool IsModifying => false;

        public string ParameterSchema => JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                includeMemory = new
                {
                    type = "boolean",
                    description = "Whether to include process memory information (optional, default: true)",
                    @default = true
                },
                includeStorage = new
                {
                    type = "boolean",
                    description = "Whether to include logical drive storage information (optional, default: true)",
                    @default = true
                }
            },
            required = Array.Empty<string>()
        });

        public Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var includeMemory = GetBoolParameter(context.Parameters, "includeMemory", true);
                var includeStorage = GetBoolParameter(context.Parameters, "includeStorage", true);

                var result = new SystemInfoResult
                {
                    MachineName = SafeValue(() => Environment.MachineName, "unavailable"),
                    OperatingSystem = SafeValue(() => RuntimeInformation.OSDescription, "unavailable"),
                    OsPlatform = DetectOsPlatform(),
                    Architecture = SafeValue(() => RuntimeInformation.OSArchitecture.ToString(), "unavailable"),
                    ProcessorCount = SafeValue(() => Environment.ProcessorCount, 0),
                    FrameworkDescription = SafeValue(() => RuntimeInformation.FrameworkDescription, "unavailable"),
                    RuntimeVersion = SafeValue(() => Environment.Version.ToString(), "unavailable")
                };

                if (includeMemory)
                {
                    result.Memory = CollectMemoryInfo();
                }

                if (includeStorage)
                {
                    result.Storage = CollectStorageInfo(cancellationToken);
                }

                var executionTime = DateTime.UtcNow - startTime;
                var message = $"Collected system information for {result.MachineName}";

                return Task.FromResult(ToolResult.Success(result, message, executionTime));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Failure($"Unexpected error collecting system info: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
        }

        public ToolValidationResult ValidateParameters(Dictionary<string, object> parameters)
        {
            var errors = new List<string>();

            if (parameters.TryGetValue("includeMemory", out var memObj))
            {
                if (!IsValidBoolean(memObj))
                {
                    errors.Add("Parameter 'includeMemory' must be a boolean");
                }
            }

            if (parameters.TryGetValue("includeStorage", out var storageObj))
            {
                if (!IsValidBoolean(storageObj))
                {
                    errors.Add("Parameter 'includeStorage' must be a boolean");
                }
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid();
        }

        public string GetExecutionPreview(Dictionary<string, object> parameters)
        {
            var includeMemory = GetBoolParameter(parameters, "includeMemory", true);
            var includeStorage = GetBoolParameter(parameters, "includeStorage", true);

            var sections = new List<string> { "OS", "runtime" };
            if (includeMemory)
            {
                sections.Add("memory");
            }
            if (includeStorage)
            {
                sections.Add("storage");
            }

            return $"Collect system info ({string.Join(", ", sections)})";
        }

        private static SystemMemoryInfo? CollectMemoryInfo()
        {
            try
            {
                // Note: this is the GC/process memory load, not total OS physical memory.
                // It is labeled accordingly in the result so consumers are not misled.
                var gcInfo = GC.GetGCMemoryInfo();
                return new SystemMemoryInfo
                {
                    Scope = "GC / process",
                    TotalManagedMemoryBytes = gcInfo.TotalAvailableMemoryBytes,
                    AllocatedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false),
                    MemoryLoadBytes = gcInfo.MemoryLoadBytes,
                    HeapSizeBytes = gcInfo.HeapSizeBytes
                };
            }
            catch (Exception ex)
            {
                return new SystemMemoryInfo
                {
                    Scope = "GC / process",
                    Error = $"unavailable: {ex.Message}"
                };
            }
        }

        private static List<DriveStorageInfo> CollectStorageInfo(CancellationToken cancellationToken)
        {
            var drives = new List<DriveStorageInfo>();

            DriveInfo[] allDrives;
            try
            {
                allDrives = DriveInfo.GetDrives();
            }
            catch (Exception ex)
            {
                drives.Add(new DriveStorageInfo
                {
                    Name = "(unknown)",
                    Error = $"unavailable: {ex.Message}"
                });
                return drives;
            }

            foreach (var drive in allDrives)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var info = new DriveStorageInfo
                {
                    Name = SafeValue(() => drive.Name, "unknown"),
                    DriveType = SafeValue(() => drive.DriveType.ToString(), "unknown"),
                    IsReady = SafeValue(() => drive.IsReady, false),
                    DriveFormat = drive.IsReady ? SafeValue(() => drive.DriveFormat, "unavailable") : null
                };

                if (drive.IsReady)
                {
                    info.TotalSizeBytes = SafeValueNullable(() => drive.TotalSize);
                    info.AvailableFreeSpaceBytes = SafeValueNullable(() => drive.AvailableFreeSpace);
                    info.TotalFreeSpaceBytes = SafeValueNullable(() => drive.TotalFreeSpace);
                }

                drives.Add(info);
            }

            return drives;
        }

        private static string DetectOsPlatform()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return "Windows";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return "Linux";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return "OSX";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                {
                    return "FreeBSD";
                }
                return "Unknown";
            }
            catch (Exception ex)
            {
                return $"unavailable: {ex.Message}";
            }
        }

        private static T SafeValue<T>(Func<T> action, T fallback)
        {
            try
            {
                return action();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static T? SafeValueNullable<T>(Func<T> action) where T : struct
        {
            try
            {
                return action();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool GetBoolParameter(Dictionary<string, object> parameters, string key, bool defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value) || value is null)
                return defaultValue;

            if (value is bool b)
                return b;

            return bool.TryParse(value.ToString(), out var result) ? result : defaultValue;
        }

        private static bool IsValidBoolean(object? value)
        {
            if (value is bool)
                return true;

            return bool.TryParse(value?.ToString(), out _);
        }
    }

    /// <summary>
    /// Result data for system information collection.
    /// </summary>
    public class SystemInfoResult
    {
        public string? MachineName { get; set; }
        public string? OperatingSystem { get; set; }
        public string? OsPlatform { get; set; }
        public string? Architecture { get; set; }
        public int ProcessorCount { get; set; }
        public string? FrameworkDescription { get; set; }
        public string? RuntimeVersion { get; set; }
        public SystemMemoryInfo? Memory { get; set; }
        public List<DriveStorageInfo>? Storage { get; set; }
    }

    /// <summary>
    /// Memory information for the current process (GC heap, not total OS RAM).
    /// </summary>
    public class SystemMemoryInfo
    {
        public string? Scope { get; set; }
        public long? TotalManagedMemoryBytes { get; set; }
        public long? AllocatedMemoryBytes { get; set; }
        public long? MemoryLoadBytes { get; set; }
        public long? HeapSizeBytes { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Storage information for a single logical drive.
    /// </summary>
    public class DriveStorageInfo
    {
        public string? Name { get; set; }
        public string? DriveType { get; set; }
        public bool IsReady { get; set; }
        public string? DriveFormat { get; set; }
        public long? TotalSizeBytes { get; set; }
        public long? AvailableFreeSpaceBytes { get; set; }
        public long? TotalFreeSpaceBytes { get; set; }
        public string? Error { get; set; }
    }
}
