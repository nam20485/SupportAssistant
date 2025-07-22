using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Tools.FileSystem
{
    /// <summary>
    /// Tool for listing directory contents with filtering and metadata
    /// </summary>
    public class ListDirectoryTool : ITool
    {
        public string Name => "ListDirectory";

        public string Description => "List files and directories in a specified path with optional filtering";

        public ToolCategory Category => ToolCategory.FileSystem;

        public ToolPermissionLevel RequiredPermission => ToolPermissionLevel.Read;

        public bool RequiresApproval => false;

        public bool IsModifying => false;

        public string ParameterSchema => JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                directoryPath = new
                {
                    type = "string",
                    description = "Path to the directory to list",
                    maxLength = 260
                },
                includeFiles = new
                {
                    type = "boolean",
                    description = "Whether to include files in the listing (default: true)",
                    @default = true
                },
                includeDirectories = new
                {
                    type = "boolean",
                    description = "Whether to include subdirectories in the listing (default: true)",
                    @default = true
                },
                filePattern = new
                {
                    type = "string",
                    description = "File pattern filter (e.g., '*.txt', '*.cs') (optional)",
                    @default = "*"
                },
                includeHidden = new
                {
                    type = "boolean",
                    description = "Whether to include hidden files and directories (default: false)",
                    @default = false
                },
                includeMetadata = new
                {
                    type = "boolean",
                    description = "Whether to include file metadata (size, dates) (default: true)",
                    @default = true
                },
                maxItems = new
                {
                    type = "integer",
                    description = "Maximum number of items to return (default: 100, max: 1000)",
                    minimum = 1,
                    maximum = 1000,
                    @default = 100
                },
                sortBy = new
                {
                    type = "string",
                    description = "Sort order for results",
                    @enum = new[] { "name", "size", "created", "modified", "type" },
                    @default = "name"
                },
                sortDescending = new
                {
                    type = "boolean",
                    description = "Whether to sort in descending order (default: false)",
                    @default = false
                }
            },
            required = new[] { "directoryPath" }
        });

        public Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Extract and validate parameters
                if (!context.Parameters.TryGetValue("directoryPath", out var dirPathObj) || dirPathObj?.ToString() is not string directoryPath)
                {
                    return Task.FromResult(ToolResult.Failure("Parameter 'directoryPath' is required and must be a string"));
                }

                var includeFiles = GetBoolParameter(context.Parameters, "includeFiles", true);
                var includeDirectories = GetBoolParameter(context.Parameters, "includeDirectories", true);
                var filePattern = GetStringParameter(context.Parameters, "filePattern", "*");
                var includeHidden = GetBoolParameter(context.Parameters, "includeHidden", false);
                var includeMetadata = GetBoolParameter(context.Parameters, "includeMetadata", true);
                var maxItems = GetIntParameter(context.Parameters, "maxItems", 100);
                var sortBy = GetStringParameter(context.Parameters, "sortBy", "name");
                var sortDescending = GetBoolParameter(context.Parameters, "sortDescending", false);

                // Validate directory path
                var validationResult = ValidateDirectoryPath(directoryPath, context.WorkingDirectory);
                if (!validationResult.IsValid)
                {
                    return Task.FromResult(ToolResult.Failure($"Invalid directory path: {string.Join(", ", validationResult.Errors)}"));
                }

                var fullPath = Path.GetFullPath(directoryPath, context.WorkingDirectory);

                // Check if directory exists
                if (!Directory.Exists(fullPath))
                {
                    return Task.FromResult(ToolResult.Failure($"Directory not found: {fullPath}"));
                }

                var items = new List<DirectoryItem>();
                var totalItems = 0;
                var isTruncated = false;

                // Get directory info for metadata
                var dirInfo = new DirectoryInfo(fullPath);

                try
                {
                    // List files if requested
                    if (includeFiles)
                    {
                        var files = dirInfo.GetFiles(filePattern, SearchOption.TopDirectoryOnly);
                        
                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (!includeHidden && (file.Attributes & FileAttributes.Hidden) != 0)
                                continue;

                            if (totalItems >= maxItems)
                            {
                                isTruncated = true;
                                break;
                            }

                            var item = new DirectoryItem
                            {
                                Name = file.Name,
                                FullPath = file.FullName,
                                Type = DirectoryItemType.File,
                                Size = includeMetadata ? file.Length : null,
                                CreatedTime = includeMetadata ? file.CreationTimeUtc : null,
                                ModifiedTime = includeMetadata ? file.LastWriteTimeUtc : null,
                                IsHidden = (file.Attributes & FileAttributes.Hidden) != 0,
                                IsReadOnly = (file.Attributes & FileAttributes.ReadOnly) != 0,
                                Extension = file.Extension
                            };

                            items.Add(item);
                            totalItems++;
                        }
                    }

                    // List directories if requested and not truncated
                    if (includeDirectories && !isTruncated)
                    {
                        var directories = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                        
                        foreach (var dir in directories)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (!includeHidden && (dir.Attributes & FileAttributes.Hidden) != 0)
                                continue;

                            if (totalItems >= maxItems)
                            {
                                isTruncated = true;
                                break;
                            }

                            var item = new DirectoryItem
                            {
                                Name = dir.Name,
                                FullPath = dir.FullName,
                                Type = DirectoryItemType.Directory,
                                Size = null, // Directories don't have a meaningful size in this context
                                CreatedTime = includeMetadata ? dir.CreationTimeUtc : null,
                                ModifiedTime = includeMetadata ? dir.LastWriteTimeUtc : null,
                                IsHidden = (dir.Attributes & FileAttributes.Hidden) != 0,
                                IsReadOnly = (dir.Attributes & FileAttributes.ReadOnly) != 0,
                                Extension = null
                            };

                            items.Add(item);
                            totalItems++;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return Task.FromResult(ToolResult.Failure($"Access denied to directory: {fullPath}"));
                }

                // Sort items
                items = SortItems(items, sortBy, sortDescending);

                var result = new DirectoryListResult
                {
                    DirectoryPath = fullPath,
                    Items = items,
                    TotalItemCount = totalItems,
                    IsTruncated = isTruncated,
                    Filter = new DirectoryListFilter
                    {
                        FilePattern = filePattern,
                        IncludeFiles = includeFiles,
                        IncludeDirectories = includeDirectories,
                        IncludeHidden = includeHidden,
                        MaxItems = maxItems,
                        SortBy = sortBy,
                        SortDescending = sortDescending
                    }
                };

                var executionTime = DateTime.UtcNow - startTime;
                var message = $"Listed {totalItems:N0} items in {Path.GetFileName(fullPath)}";
                if (isTruncated)
                {
                    message += $" (truncated at {maxItems:N0} items)";
                }

                return Task.FromResult(ToolResult.Success(result, message, executionTime));
            }
            catch (DirectoryNotFoundException ex)
            {
                return Task.FromResult(ToolResult.Failure($"Directory not found: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
            catch (IOException ex)
            {
                return Task.FromResult(ToolResult.Failure($"IO error accessing directory: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Failure($"Unexpected error: {ex.Message}", ex, DateTime.UtcNow - startTime));
            }
        }

        public ToolValidationResult ValidateParameters(Dictionary<string, object> parameters)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate directoryPath
            if (!parameters.TryGetValue("directoryPath", out var dirPathObj) || dirPathObj?.ToString() is not string directoryPath)
            {
                errors.Add("Parameter 'directoryPath' is required and must be a string");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    errors.Add("Parameter 'directoryPath' cannot be empty");
                }
                else if (directoryPath.Length > 260)
                {
                    errors.Add("Parameter 'directoryPath' is too long (max: 260 characters)");
                }
                else if (ContainsInvalidPathChars(directoryPath))
                {
                    errors.Add("Parameter 'directoryPath' contains invalid characters");
                }
            }

            // Validate maxItems
            if (parameters.TryGetValue("maxItems", out var maxItemsObj))
            {
                if (!int.TryParse(maxItemsObj?.ToString(), out var maxItems) || maxItems < 1 || maxItems > 1000)
                {
                    errors.Add("Parameter 'maxItems' must be an integer between 1 and 1000");
                }
                else if (maxItems > 500)
                {
                    warnings.Add("Large maxItems value may impact performance");
                }
            }

            // Validate sortBy
            if (parameters.TryGetValue("sortBy", out var sortByObj))
            {
                var sortBy = sortByObj?.ToString();
                if (!string.IsNullOrEmpty(sortBy) && !IsValidSortBy(sortBy))
                {
                    errors.Add("Parameter 'sortBy' must be one of: name, size, created, modified, type");
                }
            }

            // Validate filePattern
            if (parameters.TryGetValue("filePattern", out var patternObj))
            {
                var pattern = patternObj?.ToString();
                if (!string.IsNullOrEmpty(pattern) && ContainsInvalidPathChars(pattern))
                {
                    errors.Add("Parameter 'filePattern' contains invalid characters");
                }
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid(warnings);
        }

        public string GetExecutionPreview(Dictionary<string, object> parameters)
        {
            var directoryPath = GetStringParameter(parameters, "directoryPath", "");
            var filePattern = GetStringParameter(parameters, "filePattern", "*");
            var maxItems = GetIntParameter(parameters, "maxItems", 100);
            var includeFiles = GetBoolParameter(parameters, "includeFiles", true);
            var includeDirectories = GetBoolParameter(parameters, "includeDirectories", true);

            var itemTypes = new List<string>();
            if (includeFiles) itemTypes.Add("files");
            if (includeDirectories) itemTypes.Add("directories");

            var typeFilter = itemTypes.Count > 0 ? string.Join(" and ", itemTypes) : "items";
            var pattern = filePattern != "*" ? $" matching '{filePattern}'" : "";

            return $"List {typeFilter} in '{directoryPath}'{pattern} (max {maxItems:N0} items)";
        }

        private static List<DirectoryItem> SortItems(List<DirectoryItem> items, string sortBy, bool descending)
        {
            var sorted = sortBy.ToLowerInvariant() switch
            {
                "name" => items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                "size" => items.OrderBy(i => i.Size ?? 0),
                "created" => items.OrderBy(i => i.CreatedTime ?? DateTime.MinValue),
                "modified" => items.OrderBy(i => i.ModifiedTime ?? DateTime.MinValue),
                "type" => items.OrderBy(i => i.Type).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                _ => items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            };

            return descending ? sorted.Reverse().ToList() : sorted.ToList();
        }

        private static string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value))
                return defaultValue;

            return value?.ToString() ?? defaultValue;
        }

        private static bool GetBoolParameter(Dictionary<string, object> parameters, string key, bool defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value))
                return defaultValue;

            return value switch
            {
                bool boolValue => boolValue,
                string stringValue => bool.TryParse(stringValue, out var result) ? result : defaultValue,
                _ => defaultValue
            };
        }

        private static int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value))
                return defaultValue;

            return int.TryParse(value?.ToString(), out var result) ? result : defaultValue;
        }

        private static ToolValidationResult ValidateDirectoryPath(string directoryPath, string workingDirectory)
        {
            var errors = new List<string>();

            try
            {
                var fullPath = Path.GetFullPath(directoryPath, workingDirectory);
                
                // Check for path traversal attacks - allow reading from system directories
                if (!fullPath.StartsWith(Path.GetFullPath(workingDirectory)))
                {
                    var allowedRoots = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        Path.GetTempPath()
                    };

                    var isAllowed = false;
                    foreach (var root in allowedRoots)
                    {
                        if (!string.IsNullOrEmpty(root) && fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        {
                            isAllowed = true;
                            break;
                        }
                    }

                    if (!isAllowed)
                    {
                        errors.Add("Path traversal outside of allowed directories is not permitted");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid path: {ex.Message}");
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid();
        }

        private static bool ContainsInvalidPathChars(string path)
        {
            var invalidChars = Path.GetInvalidPathChars();
            return path.IndexOfAny(invalidChars) >= 0;
        }

        private static bool IsValidSortBy(string sortBy)
        {
            var validSorts = new[] { "name", "size", "created", "modified", "type" };
            return Array.Exists(validSorts, s => string.Equals(s, sortBy, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Type of directory item
    /// </summary>
    public enum DirectoryItemType
    {
        File,
        Directory
    }

    /// <summary>
    /// Represents an item in a directory listing
    /// </summary>
    public class DirectoryItem
    {
        /// <summary>
        /// Name of the file or directory
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Full path to the item
        /// </summary>
        public required string FullPath { get; set; }

        /// <summary>
        /// Type of item (file or directory)
        /// </summary>
        public DirectoryItemType Type { get; set; }

        /// <summary>
        /// Size in bytes (null for directories)
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        /// Creation time (UTC)
        /// </summary>
        public DateTime? CreatedTime { get; set; }

        /// <summary>
        /// Last modified time (UTC)
        /// </summary>
        public DateTime? ModifiedTime { get; set; }

        /// <summary>
        /// Whether the item is hidden
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Whether the item is read-only
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// File extension (null for directories)
        /// </summary>
        public string? Extension { get; set; }
    }

    /// <summary>
    /// Filter criteria used for directory listing
    /// </summary>
    public class DirectoryListFilter
    {
        public required string FilePattern { get; set; }
        public bool IncludeFiles { get; set; }
        public bool IncludeDirectories { get; set; }
        public bool IncludeHidden { get; set; }
        public int MaxItems { get; set; }
        public required string SortBy { get; set; }
        public bool SortDescending { get; set; }
    }

    /// <summary>
    /// Result data for directory listing operations
    /// </summary>
    public class DirectoryListResult
    {
        /// <summary>
        /// Full path to the directory that was listed
        /// </summary>
        public required string DirectoryPath { get; set; }

        /// <summary>
        /// Items found in the directory
        /// </summary>
        public required List<DirectoryItem> Items { get; set; }

        /// <summary>
        /// Total number of items found
        /// </summary>
        public int TotalItemCount { get; set; }

        /// <summary>
        /// Whether the result was truncated due to maxItems limit
        /// </summary>
        public bool IsTruncated { get; set; }

        /// <summary>
        /// Filter criteria that were applied
        /// </summary>
        public required DirectoryListFilter Filter { get; set; }
    }
}