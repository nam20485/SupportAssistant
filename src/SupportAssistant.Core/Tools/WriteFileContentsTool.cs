using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Tools
{
    /// <summary>
    /// Tool for writing content to a file. This is a modifying operation that
    /// requires user approval. Backup/approval are owned by the SecurityManager;
    /// this tool only performs the write itself.
    /// </summary>
    public class WriteFileContentsTool : ITool
    {
        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

        public string Name => "WriteFileContents";

        public string Description => "Write text content to a file (overwrite or append)";

        public ToolCategory Category => ToolCategory.FileSystem;

        public ToolPermissionLevel RequiredPermission => ToolPermissionLevel.Administrator;

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
                    description = "Text content to write to the file"
                },
                encoding = new
                {
                    type = "string",
                    description = "Text encoding to use (optional, default: UTF-8)",
                    @enum = new[] { "UTF-8", "ASCII", "UTF-16", "UTF-32" },
                    @default = "UTF-8"
                },
                append = new
                {
                    type = "boolean",
                    description = "Whether to append to the file instead of overwriting it (optional, default: false)",
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
                if (!context.Parameters.TryGetValue("filePath", out var filePathObj) || filePathObj?.ToString() is not string filePath)
                {
                    return ToolResult.Failure("Parameter 'filePath' is required and must be a string");
                }

                if (!context.Parameters.TryGetValue("content", out var contentObj) || contentObj?.ToString() is not string content)
                {
                    return ToolResult.Failure("Parameter 'content' is required and must be a string");
                }

                var encodingName = GetStringParameter(context.Parameters, "encoding", "UTF-8");
                var append = GetBoolParameter(context.Parameters, "append", false);

                var fullPath = Path.GetFullPath(filePath, context.WorkingDirectory);

                if (ContainsInvalidPathChars(filePath))
                {
                    return ToolResult.Failure("Parameter 'filePath' contains invalid characters");
                }

                var encoding = GetEncoding(encodingName);
                var contentBytes = encoding.GetBytes(content);

                if (contentBytes.LongLength > MaxFileSizeBytes)
                {
                    return ToolResult.Failure($"Content is too large: {contentBytes.LongLength:N0} bytes (max: {MaxFileSizeBytes:N0})");
                }

                // Create the parent directory if it does not exist.
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                var fileExistedBefore = File.Exists(fullPath);

                // Default StreamWriter behavior adds a UTF-8/Unicode BOM for those encodings,
                // which is rarely desirable for agent-authored text. Write the raw bytes directly
                // so the exact content (no preamble) is what lands on disk.
                var mode = append ? FileMode.Append : FileMode.Create;
                var fileOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;

                await using (var stream = new FileStream(fullPath, mode, FileAccess.Write, FileShare.None, bufferSize: 4096, fileOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await stream.WriteAsync(contentBytes, cancellationToken);
                }

                var executionTime = DateTime.UtcNow - startTime;
                var modifiedFiles = new List<string> { fullPath };
                var action = append ? "Appended" : "Wrote";
                var target = fileExistedBefore ? "existing file" : "new file";
                var message = $"{action} {contentBytes.Length:N0} bytes to {target}: {Path.GetFileName(fullPath)}";

                return new ToolResult(
                    isSuccess: true,
                    data: new
                    {
                        filePath = fullPath,
                        bytesWritten = contentBytes.Length,
                        append,
                        encoding = encodingName,
                        createdNewFile = !fileExistedBefore
                    },
                    message: message,
                    executionTime: executionTime,
                    modifiedFiles: modifiedFiles);
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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ToolResult.Failure($"Unexpected error: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
        }

        public ToolValidationResult ValidateParameters(Dictionary<string, object> parameters)
        {
            var errors = new List<string>();

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

            if (!parameters.TryGetValue("content", out var contentObj) || contentObj?.ToString() is not string content)
            {
                errors.Add("Parameter 'content' is required and must be a string");
            }
            else if (string.IsNullOrEmpty(content))
            {
                errors.Add("Parameter 'content' cannot be empty");
            }

            if (parameters.TryGetValue("encoding", out var encodingObj))
            {
                var encodingName = encodingObj?.ToString();
                if (!string.IsNullOrEmpty(encodingName) && !IsValidEncoding(encodingName))
                {
                    errors.Add("Parameter 'encoding' must be one of: UTF-8, ASCII, UTF-16, UTF-32");
                }
            }

            if (parameters.TryGetValue("append", out var appendObj))
            {
                if (!IsValidBoolean(appendObj))
                {
                    errors.Add("Parameter 'append' must be a boolean");
                }
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid();
        }

        public string GetExecutionPreview(Dictionary<string, object> parameters)
        {
            var filePath = GetStringParameter(parameters, "filePath", "");
            var append = GetBoolParameter(parameters, "append", false);
            var encoding = GetStringParameter(parameters, "encoding", "UTF-8");

            var action = append ? "Append to" : "Write";
            return $"{action} file '{filePath}' ({encoding} encoding)";
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

        private static Encoding GetEncoding(string encodingName)
        {
            return encodingName.ToUpperInvariant() switch
            {
                "UTF-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                "ASCII" => Encoding.ASCII,
                "UTF-16" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
                "UTF-32" => new UTF32Encoding(bigEndian: false, byteOrderMark: false),
                _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            };
        }

        private static string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value))
                return defaultValue;

            return value?.ToString() ?? defaultValue;
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
}
