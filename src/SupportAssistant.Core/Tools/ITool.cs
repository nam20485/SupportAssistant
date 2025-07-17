using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SupportAssistant.Core.Tools
{
    /// <summary>
    /// Defines the contract for all system tools that can be executed by the AI agent.
    /// Tools represent discrete actions that the assistant can perform on behalf of the user.
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// Unique identifier for this tool
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Human-readable description of what this tool does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Category of tool for permission and grouping purposes
        /// </summary>
        ToolCategory Category { get; }

        /// <summary>
        /// Required permission level to execute this tool
        /// </summary>
        ToolPermissionLevel RequiredPermission { get; }

        /// <summary>
        /// Whether this tool requires human approval before execution
        /// </summary>
        bool RequiresApproval { get; }

        /// <summary>
        /// Whether this tool can modify system state
        /// </summary>
        bool IsModifying { get; }

        /// <summary>
        /// JSON schema describing the parameters this tool accepts
        /// </summary>
        string ParameterSchema { get; }

        /// <summary>
        /// Execute the tool with the provided parameters
        /// </summary>
        /// <param name="context">Execution context with parameters and security information</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Tool execution result</returns>
        Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate parameters before execution
        /// </summary>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>Validation result with any errors</returns>
        ToolValidationResult ValidateParameters(Dictionary<string, object> parameters);

        /// <summary>
        /// Get a preview of what this tool will do with the given parameters
        /// </summary>
        /// <param name="parameters">Parameters for the operation</param>
        /// <returns>Human-readable description of the planned action</returns>
        string GetExecutionPreview(Dictionary<string, object> parameters);
    }

    /// <summary>
    /// Categories for organizing and controlling tool access
    /// </summary>
    public enum ToolCategory
    {
        /// <summary>
        /// Tools that only read information without making changes
        /// </summary>
        Information,

        /// <summary>
        /// Tools that modify files and directories
        /// </summary>
        FileSystem,

        /// <summary>
        /// Tools that modify system configuration
        /// </summary>
        Configuration,

        /// <summary>
        /// Tools that interact with the network
        /// </summary>
        Network,

        /// <summary>
        /// Tools that modify Windows registry
        /// </summary>
        Registry,

        /// <summary>
        /// Tools that interact with system services
        /// </summary>
        System,

        /// <summary>
        /// Administrative tools requiring elevated privileges
        /// </summary>
        Administrative
    }

    /// <summary>
    /// Permission levels for tool execution
    /// </summary>
    public enum ToolPermissionLevel
    {
        /// <summary>
        /// Basic read-only operations
        /// </summary>
        Read,

        /// <summary>
        /// Standard user-level modifications
        /// </summary>
        User,

        /// <summary>
        /// Elevated modifications that affect system state
        /// </summary>
        Elevated,

        /// <summary>
        /// Administrative operations requiring full privileges
        /// </summary>
        Administrator
    }

    /// <summary>
    /// Context information for tool execution
    /// </summary>
    public class ToolExecutionContext
    {
        /// <summary>
        /// Parameters for the tool execution
        /// </summary>
        public Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// User who authorized this execution
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Unique identifier for this execution session
        /// </summary>
        public string ExecutionId { get; }

        /// <summary>
        /// Timestamp when execution was initiated
        /// </summary>
        public DateTime ExecutionTime { get; }

        /// <summary>
        /// Whether user approval was obtained for this execution
        /// </summary>
        public bool UserApproved { get; }

        /// <summary>
        /// Working directory for file operations
        /// </summary>
        public string WorkingDirectory { get; }

        /// <summary>
        /// Maximum execution time allowed
        /// </summary>
        public TimeSpan Timeout { get; }

        public ToolExecutionContext(
            Dictionary<string, object> parameters,
            string userId,
            string executionId,
            bool userApproved = false,
            string? workingDirectory = null,
            TimeSpan? timeout = null)
        {
            Parameters = parameters ?? new Dictionary<string, object>();
            UserId = userId;
            ExecutionId = executionId;
            ExecutionTime = DateTime.UtcNow;
            UserApproved = userApproved;
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
            Timeout = timeout ?? TimeSpan.FromMinutes(5);
        }
    }

    /// <summary>
    /// Result of tool execution
    /// </summary>
    public class ToolResult
    {
        /// <summary>
        /// Whether the tool executed successfully
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Output data from the tool execution
        /// </summary>
        public object? Data { get; }

        /// <summary>
        /// Human-readable message about the execution result
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Error information if execution failed
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Exception that occurred during execution, if any
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Time taken to execute the tool
        /// </summary>
        public TimeSpan ExecutionTime { get; }

        /// <summary>
        /// Any files that were modified during execution
        /// </summary>
        public List<string> ModifiedFiles { get; }

        /// <summary>
        /// Backup information for rollback purposes
        /// </summary>
        public ToolBackupInfo? BackupInfo { get; }

        public ToolResult(
            bool isSuccess,
            object? data = null,
            string? message = null,
            string? errorMessage = null,
            Exception? exception = null,
            TimeSpan? executionTime = null,
            List<string>? modifiedFiles = null,
            ToolBackupInfo? backupInfo = null)
        {
            IsSuccess = isSuccess;
            Data = data;
            Message = message;
            ErrorMessage = errorMessage;
            Exception = exception;
            ExecutionTime = executionTime ?? TimeSpan.Zero;
            ModifiedFiles = modifiedFiles ?? new List<string>();
            BackupInfo = backupInfo;
        }

        /// <summary>
        /// Create a successful result
        /// </summary>
        public static ToolResult Success(object? data = null, string? message = null, TimeSpan? executionTime = null)
        {
            return new ToolResult(true, data, message, executionTime: executionTime);
        }

        /// <summary>
        /// Create a failed result
        /// </summary>
        public static ToolResult Failure(string errorMessage, Exception? exception = null, TimeSpan? executionTime = null)
        {
            return new ToolResult(false, errorMessage: errorMessage, exception: exception, executionTime: executionTime);
        }
    }

    /// <summary>
    /// Result of parameter validation
    /// </summary>
    public class ToolValidationResult
    {
        /// <summary>
        /// Whether validation passed
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Validation error messages
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Validation warnings
        /// </summary>
        public List<string> Warnings { get; }

        public ToolValidationResult(bool isValid, List<string>? errors = null, List<string>? warnings = null)
        {
            IsValid = isValid;
            Errors = errors ?? new List<string>();
            Warnings = warnings ?? new List<string>();
        }

        /// <summary>
        /// Create a successful validation result
        /// </summary>
        public static ToolValidationResult Valid(List<string>? warnings = null)
        {
            return new ToolValidationResult(true, warnings: warnings);
        }

        /// <summary>
        /// Create a failed validation result
        /// </summary>
        public static ToolValidationResult Invalid(params string[] errors)
        {
            return new ToolValidationResult(false, new List<string>(errors));
        }
    }

    /// <summary>
    /// Information about backups created during tool execution
    /// </summary>
    public class ToolBackupInfo
    {
        /// <summary>
        /// Unique identifier for this backup
        /// </summary>
        public string BackupId { get; }

        /// <summary>
        /// Timestamp when backup was created
        /// </summary>
        public DateTime BackupTime { get; }

        /// <summary>
        /// Files included in the backup
        /// </summary>
        public List<string> BackedUpFiles { get; }

        /// <summary>
        /// Registry keys backed up
        /// </summary>
        public List<string> BackedUpRegistryKeys { get; }

        /// <summary>
        /// Description of what was backed up
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Whether this backup can be automatically restored
        /// </summary>
        public bool CanRestore { get; }

        public ToolBackupInfo(
            string backupId,
            DateTime backupTime,
            List<string>? backedUpFiles = null,
            List<string>? backedUpRegistryKeys = null,
            string? description = null,
            bool canRestore = true)
        {
            BackupId = backupId;
            BackupTime = backupTime;
            BackedUpFiles = backedUpFiles ?? new List<string>();
            BackedUpRegistryKeys = backedUpRegistryKeys ?? new List<string>();
            Description = description;
            CanRestore = canRestore;
        }
    }
}
