using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Tools.FileSystem
{
    /// <summary>
    /// Tool for safely writing content to files with backup and validation
    /// </summary>
    public class WriteFileTool : ITool
    {
        public string Name => "WriteFile";

        public string Description => "Write content to a file with automatic backup and validation";

        public ToolCategory Category => ToolCategory.FileSystem;

        public ToolPermissionLevel RequiredPermission => ToolPermissionLevel.User;

        public bool RequiresApproval => true;

        public bool IsModifying => true;

        public string ParameterSchema => JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                filePath = new
                {
                    type = "string",
                    description = "Path to the file to write",
                    maxLength = 260
                },
                content = new
                {
                    type = "string",
                    description = "Content to write to the file"
                },
                encoding = new
                {
                    type = "string",
                    description = "Text encoding to use (optional, default: UTF-8)",
                    @enum = new[] { "UTF-8", "ASCII", "UTF-16", "UTF-32" },
                    @default = "UTF-8"
                },
                createBackup = new
                {
                    type = "boolean",
                    description = "Whether to create a backup of existing file (default: true)",
                    @default = true
                },
                overwrite = new
                {
                    type = "boolean",
                    description = "Whether to overwrite existing file (default: false)",
                    @default = false
                }
            },
            required = new[] { "filePath", "content" }
        });

        public async Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Extract and validate parameters
                if (!context.Parameters.TryGetValue("filePath", out var filePathObj) || filePathObj?.ToString() is not string filePath)
                {
                    return ToolResult.Failure("Parameter 'filePath' is required and must be a string");
                }

                if (!context.Parameters.TryGetValue("content", out var contentObj) || contentObj?.ToString() is not string content)
                {
                    return ToolResult.Failure("Parameter 'content' is required and must be a string");
                }

                var encodingName = GetStringParameter(context.Parameters, "encoding", "UTF-8");
                var createBackup = GetBoolParameter(context.Parameters, "createBackup", true);
                var overwrite = GetBoolParameter(context.Parameters, "overwrite", false);

                // Validate file path
                var validationResult = ValidateFilePath(filePath, context.WorkingDirectory);
                if (!validationResult.IsValid)
                {
                    return ToolResult.Failure($"Invalid file path: {string.Join(", ", validationResult.Errors)}");
                }

                var fullPath = Path.GetFullPath(filePath, context.WorkingDirectory);
                var fileExists = File.Exists(fullPath);

                // Check overwrite permission
                if (fileExists && !overwrite)
                {
                    return ToolResult.Failure($"File already exists and overwrite is disabled: {fullPath}");
                }

                // Validate content size (10MB limit)
                var encoding = GetEncoding(encodingName);
                var contentBytes = encoding.GetBytes(content);
                if (contentBytes.Length > 10 * 1024 * 1024)
                {
                    return ToolResult.Failure($"Content is too large: {contentBytes.Length:N0} bytes (max: 10MB)");
                }

                var modifiedFiles = new List<string>();
                ToolBackupInfo? backupInfo = null;

                // Create backup if requested and file exists
                if (fileExists && createBackup)
                {
                    var backupId = Guid.NewGuid().ToString("N")[..8];
                    var backupPath = $"{fullPath}.backup.{backupId}.{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                    
                    File.Copy(fullPath, backupPath, true);
                    
                    backupInfo = new ToolBackupInfo(
                        backupId,
                        DateTime.UtcNow,
                        new List<string> { fullPath },
                        description: $"Backup of {Path.GetFileName(fullPath)} before write operation",
                        canRestore: true
                    );
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write file content
                await File.WriteAllTextAsync(fullPath, content, encoding, cancellationToken);
                modifiedFiles.Add(fullPath);

                var result = new FileWriteResult
                {
                    FilePath = fullPath,
                    ContentLength = content.Length,
                    BytesWritten = contentBytes.Length,
                    Encoding = encodingName,
                    BackupCreated = backupInfo != null,
                    BackupPath = backupInfo != null ? $"{fullPath}.backup.{backupInfo.BackupId}.{DateTime.UtcNow:yyyyMMdd-HHmmss}" : null,
                    FileExisted = fileExists
                };

                var executionTime = DateTime.UtcNow - startTime;
                var message = fileExists 
                    ? $"Successfully updated file {Path.GetFileName(fullPath)} ({content.Length:N0} characters)"
                    : $"Successfully created file {Path.GetFileName(fullPath)} ({content.Length:N0} characters)";

                if (backupInfo != null)
                {
                    message += " (backup created)";
                }

                return new ToolResult(
                    true,
                    result,
                    message,
                    executionTime: executionTime,
                    modifiedFiles: modifiedFiles,
                    backupInfo: backupInfo
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                return ToolResult.Failure($"Access denied: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
            catch (DirectoryNotFoundException ex)
            {
                return ToolResult.Failure($"Directory not found: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
            catch (IOException ex)
            {
                return ToolResult.Failure($"IO error writing file: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                return ToolResult.Failure($"Unexpected error: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
        }

        public ToolValidationResult ValidateParameters(Dictionary<string, object> parameters)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate filePath
            if (!parameters.TryGetValue("filePath", out var filePathObj) || filePathObj?.ToString() is not string filePath)
            {
                errors.Add("Parameter 'filePath' is required and must be a string");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    errors.Add("Parameter 'filePath' cannot be empty");
                }
                else if (filePath.Length > 260)
                {
                    errors.Add("Parameter 'filePath' is too long (max: 260 characters)");
                }
                else if (ContainsInvalidPathChars(filePath))
                {
                    errors.Add("Parameter 'filePath' contains invalid characters");
                }
            }

            // Validate content
            if (!parameters.TryGetValue("content", out var contentObj) || contentObj?.ToString() is not string content)
            {
                errors.Add("Parameter 'content' is required and must be a string");
            }
            else if (content.Length > 5 * 1024 * 1024) // 5MB limit for validation
            {
                warnings.Add("Content is very large and may take time to write");
            }

            // Validate encoding
            if (parameters.TryGetValue("encoding", out var encodingObj))
            {
                var encodingName = encodingObj?.ToString();
                if (!string.IsNullOrEmpty(encodingName) && !IsValidEncoding(encodingName))
                {
                    errors.Add("Parameter 'encoding' must be one of: UTF-8, ASCII, UTF-16, UTF-32");
                }
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid(warnings);
        }

        public string GetExecutionPreview(Dictionary<string, object> parameters)
        {
            var filePath = GetStringParameter(parameters, "filePath", "");
            var content = GetStringParameter(parameters, "content", "");
            var encoding = GetStringParameter(parameters, "encoding", "UTF-8");
            var createBackup = GetBoolParameter(parameters, "createBackup", true);
            var overwrite = GetBoolParameter(parameters, "overwrite", false);

            var action = File.Exists(filePath) ? (overwrite ? "overwrite" : "update") : "create";
            var backup = createBackup && File.Exists(filePath) ? " (with backup)" : "";
            
            return $"Write to file '{filePath}' - {action} with {content.Length:N0} characters ({encoding} encoding){backup}";
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

        private static ToolValidationResult ValidateFilePath(string filePath, string workingDirectory)
        {
            var errors = new List<string>();

            try
            {
                var fullPath = Path.GetFullPath(filePath, workingDirectory);
                
                // Check for path traversal attacks - more restrictive for write operations
                if (!fullPath.StartsWith(Path.GetFullPath(workingDirectory)))
                {
                    // Only allow writing to user directories for safety
                    var allowedRoots = new[]
                    {
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
                        errors.Add("Write access outside of user directories is not permitted for security");
                    }
                }

                // Check for system/protected directories
                var protectedPaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                foreach (var protectedPath in protectedPaths)
                {
                    if (!string.IsNullOrEmpty(protectedPath) && fullPath.StartsWith(protectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Write access to system directory is not permitted: {protectedPath}");
                        break;
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

        private static bool IsValidEncoding(string encodingName)
        {
            var validEncodings = new[] { "UTF-8", "ASCII", "UTF-16", "UTF-32" };
            return Array.Exists(validEncodings, e => string.Equals(e, encodingName, StringComparison.OrdinalIgnoreCase));
        }

        private static System.Text.Encoding GetEncoding(string encodingName)
        {
            return encodingName.ToUpperInvariant() switch
            {
                "UTF-8" => System.Text.Encoding.UTF8,
                "ASCII" => System.Text.Encoding.ASCII,
                "UTF-16" => System.Text.Encoding.Unicode,
                "UTF-32" => System.Text.Encoding.UTF32,
                _ => System.Text.Encoding.UTF8
            };
        }
    }

    /// <summary>
    /// Result data for file write operations
    /// </summary>
    public class FileWriteResult
    {
        /// <summary>
        /// Full path to the file that was written
        /// </summary>
        public required string FilePath { get; set; }

        /// <summary>
        /// Length of content in characters
        /// </summary>
        public int ContentLength { get; set; }

        /// <summary>
        /// Number of bytes written to file
        /// </summary>
        public long BytesWritten { get; set; }

        /// <summary>
        /// Encoding used to write the file
        /// </summary>
        public required string Encoding { get; set; }

        /// <summary>
        /// Whether a backup was created
        /// </summary>
        public bool BackupCreated { get; set; }

        /// <summary>
        /// Path to backup file if created
        /// </summary>
        public string? BackupPath { get; set; }

        /// <summary>
        /// Whether the file existed before writing
        /// </summary>
        public bool FileExisted { get; set; }
    }
}