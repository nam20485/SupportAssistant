using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Tools
{
    /// <summary>
    /// Tool for listing the contents of a directory (files and subdirectories).
    /// Read-only: does not modify the file system.
    /// </summary>
    public class ListDirectoryTool : ITool
    {
        private const int DefaultMaxEntries = 200;
        private const int MaxAllowedEntries = 10000;

        public string Name => "ListDirectory";

        public string Description => "List files and subdirectories within a directory";

        public ToolCategory Category => ToolCategory.FileSystem;

        public ToolPermissionLevel RequiredPermission => ToolPermissionLevel.Read;

        public bool RequiresApproval => false;

        public bool IsModifying => false;

        public string ParameterSchema => JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Path to the directory to list",
                    maxLength = 260
                },
                pattern = new
                {
                    type = "string",
                    description = "Glob search pattern (optional, default: *)",
                    @default = "*"
                },
                maxEntries = new
                {
                    type = "integer",
                    description = "Maximum number of entries to return (optional, default: 200)",
                    minimum = 1,
                    maximum = MaxAllowedEntries,
                    @default = DefaultMaxEntries
                }
            },
            required = new[] { "path" }
        });

        public Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                if (!context.Parameters.TryGetValue("path", out var pathObj) || pathObj?.ToString() is not string path)
                {
                    return Task.FromResult(ToolResult.Failure("Parameter 'path' is required and must be a string"));
                }

                var pattern = GetStringParameter(context.Parameters, "pattern", "*");
                var maxEntries = GetIntParameter(context.Parameters, "maxEntries", DefaultMaxEntries);

                var fullPath = Path.GetFullPath(path, context.WorkingDirectory);

                if (!Directory.Exists(fullPath))
                {
                    return Task.FromResult(ToolResult.Failure($"Directory not found: {fullPath}", executionTime: DateTime.UtcNow - startTime));
                }

                var entries = new List<DirectoryEntryResult>();
                var isTruncated = false;

                var directoryInfo = new DirectoryInfo(fullPath);

                // Enumerate matching files and subdirectories, interleaving them so both are
                // represented even when the cap is reached.
                var files = SafeEnumerate(() => directoryInfo.GetFiles(pattern, SearchOption.TopDirectoryOnly));
                var dirs = SafeEnumerate(() => directoryInfo.GetDirectories(pattern, SearchOption.TopDirectoryOnly));

                var fileQueue = new Queue<FileInfo>(files);
                var dirQueue = new Queue<DirectoryInfo>(dirs);

                while ((fileQueue.Count > 0 || dirQueue.Count > 0) && entries.Count < maxEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (dirQueue.Count > 0)
                    {
                        var dir = dirQueue.Dequeue();
                        entries.Add(new DirectoryEntryResult
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            IsDirectory = true,
                            Size = null
                        });
                        continue;
                    }

                    var file = fileQueue.Dequeue();
                    entries.Add(new DirectoryEntryResult
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Size = file.Length
                    });
                }

                isTruncated = fileQueue.Count > 0 || dirQueue.Count > 0;

                var result = new ListDirectoryResult
                {
                    Path = fullPath,
                    Pattern = pattern,
                    Entries = entries,
                    EntryCount = entries.Count,
                    IsTruncated = isTruncated,
                    MaxEntries = maxEntries
                };

                var executionTime = DateTime.UtcNow - startTime;
                var message = $"Listed {entries.Count:N0} entries in {fullPath}";
                if (isTruncated)
                {
                    message += $" (truncated at {maxEntries:N0} entries)";
                }

                return Task.FromResult(ToolResult.Success(result, message, executionTime));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Task.FromResult(ToolResult.Failure($"Access denied: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
            catch (DirectoryNotFoundException ex)
            {
                return Task.FromResult(ToolResult.Failure($"Directory not found: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
            catch (IOException ex)
            {
                return Task.FromResult(ToolResult.Failure($"IO error listing directory: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Failure($"Unexpected error: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
        }

        public ToolValidationResult ValidateParameters(Dictionary<string, object> parameters)
        {
            var errors = new List<string>();

            if (!parameters.TryGetValue("path", out var pathObj) || pathObj?.ToString() is not string path)
            {
                errors.Add("Parameter 'path' is required and must be a string");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add("Parameter 'path' cannot be empty");
                }
                else if (path.Length > 260)
                {
                    errors.Add("Parameter 'path' is too long (max: 260 characters)");
                }
                else if (ContainsInvalidPathChars(path))
                {
                    errors.Add("Parameter 'path' contains invalid characters");
                }
            }

            if (parameters.TryGetValue("pattern", out var patternObj))
            {
                var pattern = patternObj?.ToString();
                if (string.IsNullOrEmpty(pattern))
                {
                    errors.Add("Parameter 'pattern' cannot be empty");
                }
                else if (ContainsInvalidPathChars(pattern))
                {
                    errors.Add("Parameter 'pattern' contains invalid characters");
                }
            }

            if (parameters.TryGetValue("maxEntries", out var maxEntriesObj))
            {
                if (!int.TryParse(maxEntriesObj?.ToString(), out var maxEntries) || maxEntries < 1 || maxEntries > MaxAllowedEntries)
                {
                    errors.Add($"Parameter 'maxEntries' must be an integer between 1 and {MaxAllowedEntries}");
                }
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid();
        }

        public string GetExecutionPreview(Dictionary<string, object> parameters)
        {
            var path = GetStringParameter(parameters, "path", "");
            var pattern = GetStringParameter(parameters, "pattern", "*");
            var maxEntries = GetIntParameter(parameters, "maxEntries", DefaultMaxEntries);

            return $"List directory '{path}' (pattern: {pattern}, max {maxEntries:N0} entries)";
        }

        private static IEnumerable<T> SafeEnumerate<T>(Func<IEnumerable<T>> action)
        {
            try
            {
                return action();
            }
            catch (UnauthorizedAccessException)
            {
                return Enumerable.Empty<T>();
            }
            catch (IOException)
            {
                return Enumerable.Empty<T>();
            }
        }

        private static int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value))
                return defaultValue;

            return int.TryParse(value?.ToString(), out var result) ? result : defaultValue;
        }

        private static string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value))
                return defaultValue;

            return value?.ToString() ?? defaultValue;
        }

        private static bool ContainsInvalidPathChars(string path)
        {
            var invalidChars = Path.GetInvalidPathChars();
            return path.IndexOfAny(invalidChars) >= 0;
        }
    }

    /// <summary>
    /// A single entry (file or directory) within a directory listing.
    /// </summary>
    public class DirectoryEntryResult
    {
        /// <summary>
        /// Name of the file or directory (without path).
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Full absolute path to the entry.
        /// </summary>
        public required string FullPath { get; set; }

        /// <summary>
        /// Whether this entry is a directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Size in bytes for files; null for directories.
        /// </summary>
        public long? Size { get; set; }
    }

    /// <summary>
    /// Result data for directory listing operations.
    /// </summary>
    public class ListDirectoryResult
    {
        /// <summary>
        /// Full path to the directory that was listed.
        /// </summary>
        public required string Path { get; set; }

        /// <summary>
        /// Glob pattern used to filter entries.
        /// </summary>
        public required string Pattern { get; set; }

        /// <summary>
        /// Entries returned (files and subdirectories).
        /// </summary>
        public required List<DirectoryEntryResult> Entries { get; set; }

        /// <summary>
        /// Number of entries returned.
        /// </summary>
        public int EntryCount { get; set; }

        /// <summary>
        /// Whether the result was truncated because the cap was reached.
        /// </summary>
        public bool IsTruncated { get; set; }

        /// <summary>
        /// Maximum number of entries that may be returned.
        /// </summary>
        public int MaxEntries { get; set; }
    }
}
