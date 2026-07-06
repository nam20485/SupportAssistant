using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Security
{
    /// <summary>
    /// Manages security and permissions for tool execution
    /// </summary>
    public interface ISecurityManager
    {
        /// <summary>
        /// Check if a user has permission to execute a tool
        /// </summary>
        Task<bool> HasPermissionAsync(string userId, ITool tool);

        /// <summary>
        /// Request user approval for tool execution
        /// </summary>
        Task<ToolApprovalResult> RequestApprovalAsync(string userId, ITool tool, Dictionary<string, object> parameters);

        /// <summary>
        /// Create a backup before executing a modifying tool
        /// </summary>
        Task<ToolBackupInfo> CreateBackupAsync(ITool tool, Dictionary<string, object> parameters);

        /// <summary>
        /// Restore from a backup
        /// </summary>
        Task<bool> RestoreBackupAsync(string backupId);

        /// <summary>
        /// Log tool execution for audit purposes
        /// </summary>
        Task LogExecutionAsync(ToolExecutionAuditEntry entry);

        /// <summary>
        /// Get user's current permission level
        /// </summary>
        ToolPermissionLevel GetUserPermissionLevel(string userId);

        /// <summary>
        /// Set user's permission level
        /// </summary>
        Task SetUserPermissionLevelAsync(string userId, ToolPermissionLevel permissionLevel);

        /// <summary>
        /// Check if a tool execution should be allowed based on security policies
        /// </summary>
        Task<SecurityCheckResult> ValidateExecutionAsync(string userId, ITool tool, Dictionary<string, object> parameters);

        /// <summary>
        /// Get audit trail for tool executions
        /// </summary>
        Task<IEnumerable<ToolExecutionAuditEntry>> GetAuditTrailAsync(string? userId = null, DateTime? fromDate = null, DateTime? toDate = null);
    }
}
