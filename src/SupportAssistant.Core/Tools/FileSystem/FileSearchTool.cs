using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Tools.FileSystem
{
    /// <summary>
    /// Tool for searching files based on name patterns, content, and metadata
    /// </summary>
    public class FileSearchTool : ITool
    {
        public string Name => "FileSearch";

        public string Description => "Search for files by name pattern, content, or metadata in specified directories";

        public ToolCategory Category => ToolCategory.FileSystem;

        public ToolPermissionLevel RequiredPermission => ToolPermissionLevel.Read;

        public bool RequiresApproval => false;

        public bool IsModifying => false;

        public string ParameterSchema => JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                searchPath = new
                {
                    type = "string",
                    description = "Root directory to search in",
                    maxLength = 260
                },
                namePattern = new
                {
                    type = "string",
                    description = "File name pattern (e.g., '*.txt', 'config.*') (optional)",
                    @default = "*"
                },
                contentPattern = new
                {
                    type = "string",
                    description = "Text pattern to search for within files (optional)"
                },
                recursive = new
                {
                    type = "boolean",
                    description = "Whether to search subdirectories recursively (default: false)",
                    @default = false
                },
                maxDepth = new
                {
                    type = "integer",
                    description = "Maximum directory depth for recursive search (default: 3, max: 10)",
                    minimum = 1,
                    maximum = 10,
                    @default = 3
                },
                caseSensitive = new
                {
                    type = "boolean",
                    description = "Whether content search is case-sensitive (default: false)",
                    @default = false
                },
                includeHidden = new
                {
                    type = "boolean",
                    description = "Whether to include hidden files (default: false)",
                    @default = false
                },
                minSize = new
                {
                    type = "integer",
                    description = "Minimum file size in bytes (optional)",
                    minimum = 0
                },
                maxSize = new
                {
                    type = "integer",
                    description = "Maximum file size in bytes (optional, default: 100MB)",
                    minimum = 1,
                    @default = 104857600
                },
                maxResults = new
                {
                    type = "integer",
                    description = "Maximum number of results to return (default: 50, max: 500)",
                    minimum = 1,
                    maximum = 500,
                    @default = 50
                },
                fileExtensions = new
                {
                    type = "array",
                    description = "Specific file extensions to search (e.g., ['.txt', '.log']) (optional)",
                    items = new { type = "string" }
                }
            },
            required = new[] { "searchPath" }
        });

        public async Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Extract and validate parameters
                if (!context.Parameters.TryGetValue("searchPath", out var searchPathObj) || searchPathObj?.ToString() is not string searchPath)
                {
                    return ToolResult.Failure("Parameter 'searchPath' is required and must be a string");
                }

                var namePattern = GetStringParameter(context.Parameters, "namePattern", "*");
                var contentPattern = GetStringParameter(context.Parameters, "contentPattern", "");
                var recursive = GetBoolParameter(context.Parameters, "recursive", false);
                var maxDepth = GetIntParameter(context.Parameters, "maxDepth", 3);
                var caseSensitive = GetBoolParameter(context.Parameters, "caseSensitive", false);
                var includeHidden = GetBoolParameter(context.Parameters, "includeHidden", false);
                var minSize = GetLongParameter(context.Parameters, "minSize", 0);
                var maxSize = GetLongParameter(context.Parameters, "maxSize", 100 * 1024 * 1024);
                var maxResults = GetIntParameter(context.Parameters, "maxResults", 50);
                var fileExtensions = GetStringArrayParameter(context.Parameters, "fileExtensions");

                // Validate search path
                var validationResult = ValidateSearchPath(searchPath, context.WorkingDirectory);
                if (!validationResult.IsValid)
                {
                    return ToolResult.Failure($"Invalid search path: {string.Join(", ", validationResult.Errors)}");
                }

                var fullPath = Path.GetFullPath(searchPath, context.WorkingDirectory);

                // Check if directory exists
                if (!Directory.Exists(fullPath))
                {
                    return ToolResult.Failure($"Directory not found: {fullPath}");
                }

                var searchResults = new List<FileSearchResult>();
                var searchStats = new SearchStatistics();

                // Prepare content search regex if specified
                Regex? contentRegex = null;
                if (!string.IsNullOrEmpty(contentPattern))
                {
                    try
                    {
                        var regexOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        contentRegex = new Regex(Regex.Escape(contentPattern), regexOptions | RegexOptions.Compiled);
                    }
                    catch (ArgumentException ex)
                    {
                        return ToolResult.Failure($"Invalid content pattern: {ex.Message}");
                    }
                }

                // Search for files
                await SearchDirectoryAsync(
                    fullPath,
                    namePattern,
                    contentRegex,
                    recursive,
                    maxDepth,
                    0,
                    includeHidden,
                    minSize,
                    maxSize,
                    fileExtensions,
                    searchResults,
                    searchStats,
                    maxResults,
                    cancellationToken);

                var result = new FileSearchResponse
                {
                    SearchPath = fullPath,
                    Results = searchResults,
                    Statistics = searchStats,
                    SearchCriteria = new FileSearchCriteria
                    {
                        NamePattern = namePattern,
                        ContentPattern = contentPattern,
                        Recursive = recursive,
                        MaxDepth = maxDepth,
                        CaseSensitive = caseSensitive,
                        IncludeHidden = includeHidden,
                        MinSize = minSize,
                        MaxSize = maxSize,
                        MaxResults = maxResults,
                        FileExtensions = fileExtensions?.ToList() ?? new List<string>()
                    }
                };

                var executionTime = DateTime.UtcNow - startTime;
                var message = $"Found {searchResults.Count:N0} matching files";
                if (searchStats.IsTruncated)
                {
                    message += $" (truncated at {maxResults:N0} results)";
                }
                message += $" in {searchStats.DirectoriesSearched:N0} directories";

                return ToolResult.Success(result, message, executionTime);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ToolResult.Failure($"Access denied: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
            catch (IOException ex)
            {
                return ToolResult.Failure($"IO error during search: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
            catch (Exception ex)
            {
                return ToolResult.Failure($"Unexpected error: {ex.Message}", ex, DateTime.UtcNow - startTime);
            }
        }

        private async Task SearchDirectoryAsync(
            string directoryPath,
            string namePattern,
            Regex? contentRegex,
            bool recursive,
            int maxDepth,
            int currentDepth,
            bool includeHidden,
            long minSize,
            long maxSize,
            string[]? fileExtensions,
            List<FileSearchResult> results,
            SearchStatistics stats,
            int maxResults,
            CancellationToken cancellationToken)
        {
            if (results.Count >= maxResults)
            {
                stats.IsTruncated = true;
                return;
            }

            try
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                stats.DirectoriesSearched++;

                // Search files in current directory
                var files = dirInfo.GetFiles(namePattern, SearchOption.TopDirectoryOnly);
                
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (results.Count >= maxResults)
                    {
                        stats.IsTruncated = true;
                        break;
                    }

                    stats.FilesExamined++;

                    // Apply filters
                    if (!includeHidden && (file.Attributes & FileAttributes.Hidden) != 0)
                        continue;

                    if (file.Length < minSize || file.Length > maxSize)
                        continue;

                    if (fileExtensions != null && fileExtensions.Length > 0)
                    {
                        if (!fileExtensions.Any(ext => string.Equals(file.Extension, ext, StringComparison.OrdinalIgnoreCase)))
                            continue;
                    }

                    // Check content if pattern specified
                    bool hasContentMatch = contentRegex == null;
                    List<ContentMatch> contentMatchList = new();

                    if (contentRegex != null)
                    {
                        try
                        {
                            // Only search text files and limit file size for content search
                            if (file.Length <= 10 * 1024 * 1024 && IsTextFile(file.Extension)) // 10MB limit
                            {
                                var content = await File.ReadAllTextAsync(file.FullName, cancellationToken);
                                var matches = contentRegex.Matches(content);
                                
                                if (matches.Count > 0)
                                {
                                    hasContentMatch = true;
                                    foreach (Match match in matches.Take(10)) // Limit to 10 matches per file
                                    {
                                        var lineNumber = GetLineNumber(content, match.Index);
                                        var lineText = GetLineText(content, match.Index);
                                        
                                        contentMatchList.Add(new ContentMatch
                                        {
                                            LineNumber = lineNumber,
                                            LineText = lineText.Trim(),
                                            MatchText = match.Value,
                                            Position = match.Index
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            // Skip files that can't be read
                            continue;
                        }
                    }

                    if (hasContentMatch)
                    {
                        var searchResult = new FileSearchResult
                        {
                            FilePath = file.FullName,
                            FileName = file.Name,
                            DirectoryPath = file.DirectoryName!,
                            Size = file.Length,
                            CreatedTime = file.CreationTimeUtc,
                            ModifiedTime = file.LastWriteTimeUtc,
                            Extension = file.Extension,
                            IsHidden = (file.Attributes & FileAttributes.Hidden) != 0,
                            IsReadOnly = (file.Attributes & FileAttributes.ReadOnly) != 0,
                            ContentMatches = contentMatchList
                        };

                        results.Add(searchResult);
                        stats.MatchingFiles++;
                    }
                }

                // Search subdirectories if recursive and within depth limit
                if (recursive && currentDepth < maxDepth && !stats.IsTruncated)
                {
                    var subdirectories = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    
                    foreach (var subdir in subdirectories)
                    {
                        if (!includeHidden && (subdir.Attributes & FileAttributes.Hidden) != 0)
                            continue;

                        await SearchDirectoryAsync(
                            subdir.FullName,
                            namePattern,
                            contentRegex,
                            recursive,
                            maxDepth,
                            currentDepth + 1,
                            includeHidden,
                            minSize,
                            maxSize,
                            fileExtensions,
                            results,
                            stats,
                            maxResults,
                            cancellationToken);

                        if (stats.IsTruncated)
                            break;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
                stats.InaccessibleDirectories++;
            }
            catch (DirectoryNotFoundException)
            {
                // Skip directories that no longer exist
            }
        }

        private static bool IsTextFile(string extension)
        {
            var textExtensions = new[]
            {
                ".txt", ".log", ".ini", ".cfg", ".conf", ".config", ".xml", ".json", ".yml", ".yaml",
                ".cs", ".js", ".ts", ".html", ".htm", ".css", ".md", ".readme", ".sql", ".py",
                ".java", ".cpp", ".c", ".h", ".hpp", ".php", ".rb", ".go", ".rs", ".sh", ".bat",
                ".ps1", ".dockerfile", ".gitignore", ".gitattributes"
            };

            return Array.Exists(textExtensions, ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static int GetLineNumber(string content, int position)
        {
            return content.Take(position).Count(c => c == '\n') + 1;
        }

        private static string GetLineText(string content, int position)
        {
            var lineStart = content.LastIndexOf('\n', position - 1) + 1;
            var lineEnd = content.IndexOf('\n', position);
            if (lineEnd == -1) lineEnd = content.Length;

            return content.Substring(lineStart, lineEnd - lineStart);
        }

        public ToolValidationResult ValidateParameters(Dictionary<string, object> parameters)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate searchPath
            if (!parameters.TryGetValue("searchPath", out var searchPathObj) || searchPathObj?.ToString() is not string searchPath)
            {
                errors.Add("Parameter 'searchPath' is required and must be a string");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(searchPath))
                {
                    errors.Add("Parameter 'searchPath' cannot be empty");
                }
                else if (searchPath.Length > 260)
                {
                    errors.Add("Parameter 'searchPath' is too long (max: 260 characters)");
                }
            }

            // Validate maxResults
            if (parameters.TryGetValue("maxResults", out var maxResultsObj))
            {
                if (!int.TryParse(maxResultsObj?.ToString(), out var maxResults) || maxResults < 1 || maxResults > 500)
                {
                    errors.Add("Parameter 'maxResults' must be an integer between 1 and 500");
                }
                else if (maxResults > 200)
                {
                    warnings.Add("Large maxResults value may impact performance");
                }
            }

            // Validate maxDepth
            if (parameters.TryGetValue("maxDepth", out var maxDepthObj))
            {
                if (!int.TryParse(maxDepthObj?.ToString(), out var maxDepth) || maxDepth < 1 || maxDepth > 10)
                {
                    errors.Add("Parameter 'maxDepth' must be an integer between 1 and 10");
                }
                else if (maxDepth > 5)
                {
                    warnings.Add("Deep recursive search may impact performance");
                }
            }

            // Warn about content search performance
            if (parameters.TryGetValue("contentPattern", out var contentPatternObj) && !string.IsNullOrEmpty(contentPatternObj?.ToString()))
            {
                warnings.Add("Content search may be slow for large files or many files");
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid(warnings);
        }

        public string GetExecutionPreview(Dictionary<string, object> parameters)
        {
            var searchPath = GetStringParameter(parameters, "searchPath", "");
            var namePattern = GetStringParameter(parameters, "namePattern", "*");
            var contentPattern = GetStringParameter(parameters, "contentPattern", "");
            var recursive = GetBoolParameter(parameters, "recursive", false);
            var maxResults = GetIntParameter(parameters, "maxResults", 50);

            var recursiveText = recursive ? " (recursive)" : "";
            var contentText = !string.IsNullOrEmpty(contentPattern) ? $" containing '{contentPattern}'" : "";
            
            return $"Search for files matching '{namePattern}'{contentText} in '{searchPath}'{recursiveText} (max {maxResults:N0} results)";
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

        private static long GetLongParameter(Dictionary<string, object> parameters, string key, long defaultValue)
        {
            if (!parameters.TryGetValue(key, out var value))
                return defaultValue;

            return long.TryParse(value?.ToString(), out var result) ? result : defaultValue;
        }

        private static string[]? GetStringArrayParameter(Dictionary<string, object> parameters, string key)
        {
            if (!parameters.TryGetValue(key, out var value))
                return null;

            return value switch
            {
                string[] stringArray => stringArray,
                List<string> stringList => stringList.ToArray(),
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Array =>
                    jsonElement.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                _ => null
            };
        }

        private static ToolValidationResult ValidateSearchPath(string searchPath, string workingDirectory)
        {
            var errors = new List<string>();

            try
            {
                var fullPath = Path.GetFullPath(searchPath, workingDirectory);
                
                // Allow searching in standard directories for read operations
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
                        errors.Add("Search outside of allowed directories is not permitted");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Invalid path: {ex.Message}");
            }

            return errors.Count > 0 ? ToolValidationResult.Invalid(errors.ToArray()) : ToolValidationResult.Valid();
        }
    }

    /// <summary>
    /// Represents a content match within a file
    /// </summary>
    public class ContentMatch
    {
        public int LineNumber { get; set; }
        public required string LineText { get; set; }
        public required string MatchText { get; set; }
        public int Position { get; set; }
    }

    /// <summary>
    /// Statistics for a file search operation
    /// </summary>
    public class SearchStatistics
    {
        public int DirectoriesSearched { get; set; }
        public int FilesExamined { get; set; }
        public int MatchingFiles { get; set; }
        public int InaccessibleDirectories { get; set; }
        public bool IsTruncated { get; set; }
    }

    /// <summary>
    /// Search criteria used for the operation
    /// </summary>
    public class FileSearchCriteria
    {
        public required string NamePattern { get; set; }
        public required string ContentPattern { get; set; }
        public bool Recursive { get; set; }
        public int MaxDepth { get; set; }
        public bool CaseSensitive { get; set; }
        public bool IncludeHidden { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
        public int MaxResults { get; set; }
        public required List<string> FileExtensions { get; set; }
    }

    /// <summary>
    /// Represents a file found during search
    /// </summary>
    public class FileSearchResult
    {
        public required string FilePath { get; set; }
        public required string FileName { get; set; }
        public required string DirectoryPath { get; set; }
        public long Size { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public required string Extension { get; set; }
        public bool IsHidden { get; set; }
        public bool IsReadOnly { get; set; }
        public required List<ContentMatch> ContentMatches { get; set; }
    }

    /// <summary>
    /// Response containing search results
    /// </summary>
    public class FileSearchResponse
    {
        public required string SearchPath { get; set; }
        public required List<FileSearchResult> Results { get; set; }
        public required SearchStatistics Statistics { get; set; }
        public required FileSearchCriteria SearchCriteria { get; set; }
    }
}