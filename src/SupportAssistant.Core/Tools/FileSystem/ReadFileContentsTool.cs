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
    /// Tool for safely reading file contents
    /// </summary>
    public class ReadFileContentsTool : ITool
    {
        public string Name => "ReadFileContents";

        public string Description => "Read the contents of a text file from the file system";

        public ToolCategory Category => ToolCategory.FileSystem;

        public ToolPermissionLevel RequiredPermission => ToolPermissionLevel.Read;

        public bool RequiresApproval => false;

        public bool IsModifying => false;

        public string ParameterSchema => JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                filePath = new
                {
                    type = "string",
                    description = "Path to the file to read",
                    maxLength = 260
                },
                maxLines = new
                {
                    type = "integer",
                    description = "Maximum number of lines to read (optional, default: 1000)",
                    minimum = 1,
                    maximum = 10000,
                    @default = 1000
                },
                encoding = new
                {
                    type = "string",
                    description = "Text encoding to use (optional, default: UTF-8)",
                    @enum = new[] { "UTF-8", "ASCII", "UTF-16", "UTF-32" },
                    @default = "UTF-8"
                }
            },
            required = new[] { "filePath" }
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

                var maxLines = GetIntParameter(context.Parameters, "maxLines", 1000);
                var encodingName = GetStringParameter(context.Parameters, "encoding", "UTF-8");

                // Validate file path
                var validationResult = ValidateFilePath(filePath, context.WorkingDirectory);
                if (!validationResult.IsValid)
                {
                    return ToolResult.Failure($"Invalid file path: {string.Join(", ", validationResult.Errors)}");
                }

                var fullPath = Path.GetFullPath(filePath, context.WorkingDirectory);

                // Check if file exists
                if (!File.Exists(fullPath))
                {
                    return ToolResult.Failure($"File not found: {fullPath}");
                }

                // Get file info for metadata
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > 50 * 1024 * 1024) // 50MB limit
                {
                    return ToolResult.Failure($"File is too large: {fileInfo.Length:N0} bytes (max: 50MB)");
                }

                // Read file contents
                var encoding = GetEncoding(encodingName);
                var lines = new List<string>();
                var lineCount = 0;
                var isTruncated = false;

                using var reader = new StreamReader(fullPath, encoding);
                
                while (lineCount < maxLines && !reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        lines.Add(line);
                        lineCount++;
                    }
                }

                if (!reader.EndOfStream)
                {
                    isTruncated = true;
                }

                var result = new FileReadResult
                {
                    FilePath = fullPath,
                    Lines = lines,
                    LineCount = lineCount,
                    IsTruncated = isTruncated,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc,
                    Encoding = encodingName
                };

                var executionTime = DateTime.UtcNow - startTime;
                var message = $"Successfully read {lineCount:N0} lines from {Path.GetFileName(fullPath)}";
                if (isTruncated)
                {
                    message += $" (truncated at {maxLines:N0} lines)";
                }

                return ToolResult.Success(result, message, executionTime);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ToolResult.Failure($"Access denied: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
            catch (IOException ex)
            {
                return ToolResult.Failure($"IO error reading file: {ex.Message}", ex, DateTime.UtcNow - startTime);
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

            // Validate maxLines
            if (parameters.TryGetValue("maxLines", out var maxLinesObj))
            {
                if (!int.TryParse(maxLinesObj?.ToString(), out var maxLines) || maxLines < 1 || maxLines > 10000)
                {
                    errors.Add("Parameter 'maxLines' must be an integer between 1 and 10000");
                }
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
            var maxLines = GetIntParameter(parameters, "maxLines", 1000);
            var encoding = GetStringParameter(parameters, "encoding", "UTF-8");

            return $"Read file '{filePath}' (max {maxLines:N0} lines, {encoding} encoding)";
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

        private static ToolValidationResult ValidateFilePath(string filePath, string workingDirectory)
        {
            var errors = new List<string>();

            try
            {
                var fullPath = Path.GetFullPath(filePath, workingDirectory);
                
                // Check for path traversal attacks
                if (!fullPath.StartsWith(Path.GetFullPath(workingDirectory)))
                {
                    // Allow reading from system directories for information tools
                    var allowedRoots = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
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
    /// Result data for file read operations
    /// </summary>
    public class FileReadResult
    {
        /// <summary>
        /// Full path to the file that was read
        /// </summary>
        public required string FilePath { get; set; }

        /// <summary>
        /// Lines read from the file
        /// </summary>
        public required List<string> Lines { get; set; }

        /// <summary>
        /// Number of lines read
        /// </summary>
        public int LineCount { get; set; }

        /// <summary>
        /// Whether the file was truncated due to line limit
        /// </summary>
        public bool IsTruncated { get; set; }

        /// <summary>
        /// Size of the file in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// When the file was last modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Encoding used to read the file
        /// </summary>
        public required string Encoding { get; set; }
    }
}
