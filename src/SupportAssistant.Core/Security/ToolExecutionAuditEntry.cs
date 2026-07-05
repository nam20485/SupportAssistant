using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Security
{
    /// <summary>
    /// Audit entry for tool execution
    /// </summary>
    public class ToolExecutionAuditEntry
    {
        /// <summary>
        /// Unique identifier for this audit entry
        /// </summary>
        public string AuditId { get; }

        /// <summary>
        /// User who executed the tool
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Name of the tool that was executed
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// Parameters passed to the tool
        /// </summary>
        public Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Execution result summary
        /// </summary>
        public string ExecutionResult { get; }

        /// <summary>
        /// Whether execution was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Timestamp when execution started
        /// </summary>
        public DateTime ExecutionTime { get; }

        /// <summary>
        /// How long execution took
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Files that were modified during execution
        /// </summary>
        public List<string> ModifiedFiles { get; }

        /// <summary>
        /// Backup information if any backups were created
        /// </summary>
        public string? BackupId { get; }

        /// <summary>
        /// User's IP address or machine identifier
        /// </summary>
        public string? ClientIdentifier { get; }

        /// <summary>
        /// Whether user approval was required and obtained
        /// </summary>
        public bool RequiredApproval { get; }

        /// <summary>
        /// Approval ID if approval was obtained
        /// </summary>
        public string? ApprovalId { get; }

        public ToolExecutionAuditEntry(
            string auditId,
            string userId,
            string toolName,
            Dictionary<string, object> parameters,
            string executionResult,
            bool isSuccess,
            DateTime executionTime,
            TimeSpan duration,
            List<string>? modifiedFiles = null,
            string? backupId = null,
            string? clientIdentifier = null,
            bool requiredApproval = false,
            string? approvalId = null)
        {
            AuditId = auditId;
            UserId = userId;
            ToolName = toolName;
            Parameters = parameters;
            ExecutionResult = executionResult;
            IsSuccess = isSuccess;
            ExecutionTime = executionTime;
            Duration = duration;
            ModifiedFiles = modifiedFiles ?? new List<string>();
            BackupId = backupId;
            ClientIdentifier = clientIdentifier;
            RequiredApproval = requiredApproval;
            ApprovalId = approvalId;
        }
    }
}
