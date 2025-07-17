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

    /// <summary>
    /// Result of approval request
    /// </summary>
    public class ToolApprovalResult
    {
        /// <summary>
        /// Whether approval was granted
        /// </summary>
        public bool IsApproved { get; }

        /// <summary>
        /// Approval session ID for tracking
        /// </summary>
        public string ApprovalId { get; }

        /// <summary>
        /// Timestamp when approval was granted/denied
        /// </summary>
        public DateTime ApprovalTime { get; }

        /// <summary>
        /// User comments about the approval decision
        /// </summary>
        public string? UserComments { get; }

        /// <summary>
        /// How long this approval is valid
        /// </summary>
        public TimeSpan? ValidityDuration { get; }

        /// <summary>
        /// Whether this approval applies to similar future requests
        /// </summary>
        public bool RememberDecision { get; }

        public ToolApprovalResult(
            bool isApproved,
            string approvalId,
            DateTime approvalTime,
            string? userComments = null,
            TimeSpan? validityDuration = null,
            bool rememberDecision = false)
        {
            IsApproved = isApproved;
            ApprovalId = approvalId;
            ApprovalTime = approvalTime;
            UserComments = userComments;
            ValidityDuration = validityDuration;
            RememberDecision = rememberDecision;
        }

        public static ToolApprovalResult Approved(string approvalId, string? comments = null, TimeSpan? validity = null, bool remember = false)
        {
            return new ToolApprovalResult(true, approvalId, DateTime.UtcNow, comments, validity, remember);
        }

        public static ToolApprovalResult Denied(string approvalId, string? comments = null)
        {
            return new ToolApprovalResult(false, approvalId, DateTime.UtcNow, comments);
        }
    }

    /// <summary>
    /// Result of security validation
    /// </summary>
    public class SecurityCheckResult
    {
        /// <summary>
        /// Whether execution is allowed
        /// </summary>
        public bool IsAllowed { get; }

        /// <summary>
        /// Reason for denial if not allowed
        /// </summary>
        public string? DenialReason { get; }

        /// <summary>
        /// Required actions before execution can proceed
        /// </summary>
        public List<string> RequiredActions { get; }

        /// <summary>
        /// Security warnings that should be displayed to user
        /// </summary>
        public List<string> Warnings { get; }

        public SecurityCheckResult(
            bool isAllowed,
            string? denialReason = null,
            List<string>? requiredActions = null,
            List<string>? warnings = null)
        {
            IsAllowed = isAllowed;
            DenialReason = denialReason;
            RequiredActions = requiredActions ?? new List<string>();
            Warnings = warnings ?? new List<string>();
        }

        public static SecurityCheckResult Allow(List<string>? warnings = null)
        {
            return new SecurityCheckResult(true, warnings: warnings);
        }

        public static SecurityCheckResult Deny(string reason, List<string>? requiredActions = null)
        {
            return new SecurityCheckResult(false, reason, requiredActions);
        }
    }

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

    /// <summary>
    /// User permission settings for tools
    /// </summary>
    public class UserPermissionSettings
    {
        /// <summary>
        /// User identifier
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Overall permission level for this user
        /// </summary>
        public ToolPermissionLevel PermissionLevel { get; set; }

        /// <summary>
        /// Tool categories that this user can access
        /// </summary>
        public HashSet<ToolCategory> AllowedCategories { get; set; } = new();

        /// <summary>
        /// Specific tools that are explicitly allowed
        /// </summary>
        public HashSet<string> ExplicitlyAllowedTools { get; set; } = new();

        /// <summary>
        /// Specific tools that are explicitly denied
        /// </summary>
        public HashSet<string> ExplicitlyDeniedTools { get; set; } = new();

        /// <summary>
        /// Whether to remember approval decisions for this user
        /// </summary>
        public bool RememberApprovals { get; set; } = true;

        /// <summary>
        /// Default duration for remembered approvals
        /// </summary>
        public TimeSpan DefaultApprovalDuration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Maximum files that can be modified in a single operation
        /// </summary>
        public int MaxModifiedFiles { get; set; } = 100;

        /// <summary>
        /// Whether to require approval for all modifying operations
        /// </summary>
        public bool RequireApprovalForModifying { get; set; } = true;

        /// <summary>
        /// Whether to create automatic backups before modifications
        /// </summary>
        public bool AutoCreateBackups { get; set; } = true;
    }

    /// <summary>
    /// Default implementation of security manager
    /// </summary>
    public class SecurityManager : ISecurityManager
    {
        private readonly Dictionary<string, UserPermissionSettings> _userSettings = new();
        private readonly Dictionary<string, ToolApprovalResult> _rememberedApprovals = new();
        private readonly Dictionary<string, ToolBackupInfo> _backups = new();
        private readonly List<ToolExecutionAuditEntry> _auditTrail = new();
        private readonly object _lock = new object();

        public Task<bool> HasPermissionAsync(string userId, ITool tool)
        {
            var userSettings = GetUserSettings(userId);
            
            // Check if user's permission level is sufficient
            if (userSettings.PermissionLevel < tool.RequiredPermission)
                return Task.FromResult(false);

            // Check if tool category is allowed
            if (!userSettings.AllowedCategories.Contains(tool.Category))
                return Task.FromResult(false);

            // Check explicit denials
            if (userSettings.ExplicitlyDeniedTools.Contains(tool.Name))
                return Task.FromResult(false);

            // Check explicit approvals (overrides category restrictions)
            if (userSettings.ExplicitlyAllowedTools.Contains(tool.Name))
                return Task.FromResult(true);

            return Task.FromResult(true);
        }

        public async Task<ToolApprovalResult> RequestApprovalAsync(string userId, ITool tool, Dictionary<string, object> parameters)
        {
            // Check if we have a remembered approval
            var rememberedKey = GenerateApprovalKey(userId, tool.Name, parameters);
            
            lock (_lock)
            {
                if (_rememberedApprovals.TryGetValue(rememberedKey, out var remembered) &&
                    remembered.ValidityDuration.HasValue &&
                    remembered.ApprovalTime.Add(remembered.ValidityDuration.Value) > DateTime.UtcNow)
                {
                    return remembered;
                }
            }

            // For now, simulate user approval (in real implementation, this would show UI)
            // This is a placeholder that will be replaced with actual UI interaction
            return await SimulateUserApprovalAsync(userId, tool, parameters);
        }

        public Task<ToolBackupInfo> CreateBackupAsync(ITool tool, Dictionary<string, object> parameters)
        {
            var backupId = Guid.NewGuid().ToString();
            var backupInfo = new ToolBackupInfo(
                backupId,
                DateTime.UtcNow,
                description: $"Backup before {tool.Name} execution",
                canRestore: true
            );

            lock (_lock)
            {
                _backups[backupId] = backupInfo;
            }

            return Task.FromResult(backupInfo);
        }

        public Task<bool> RestoreBackupAsync(string backupId)
        {
            lock (_lock)
            {
                if (_backups.TryGetValue(backupId, out var backup) && backup.CanRestore)
                {
                    // Implementation would restore files/registry from backup
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }

        public Task LogExecutionAsync(ToolExecutionAuditEntry entry)
        {
            lock (_lock)
            {
                _auditTrail.Add(entry);
            }

            return Task.CompletedTask;
        }

        public ToolPermissionLevel GetUserPermissionLevel(string userId)
        {
            return GetUserSettings(userId).PermissionLevel;
        }

        public Task SetUserPermissionLevelAsync(string userId, ToolPermissionLevel permissionLevel)
        {
            var settings = GetUserSettings(userId);
            settings.PermissionLevel = permissionLevel;

            // Set default allowed categories based on permission level
            settings.AllowedCategories.Clear();
            switch (permissionLevel)
            {
                case ToolPermissionLevel.Read:
                    settings.AllowedCategories.Add(ToolCategory.Information);
                    break;
                case ToolPermissionLevel.User:
                    settings.AllowedCategories.Add(ToolCategory.Information);
                    settings.AllowedCategories.Add(ToolCategory.FileSystem);
                    settings.AllowedCategories.Add(ToolCategory.Network);
                    break;
                case ToolPermissionLevel.Elevated:
                    settings.AllowedCategories.Add(ToolCategory.Information);
                    settings.AllowedCategories.Add(ToolCategory.FileSystem);
                    settings.AllowedCategories.Add(ToolCategory.Network);
                    settings.AllowedCategories.Add(ToolCategory.Configuration);
                    settings.AllowedCategories.Add(ToolCategory.System);
                    break;
                case ToolPermissionLevel.Administrator:
                    foreach (ToolCategory category in Enum.GetValues<ToolCategory>())
                    {
                        settings.AllowedCategories.Add(category);
                    }
                    break;
            }

            return Task.CompletedTask;
        }

        public Task<SecurityCheckResult> ValidateExecutionAsync(string userId, ITool tool, Dictionary<string, object> parameters)
        {
            var warnings = new List<string>();
            var requiredActions = new List<string>();

            // Check basic permissions
            if (!HasPermissionAsync(userId, tool).Result)
            {
                return Task.FromResult(SecurityCheckResult.Deny("Insufficient permissions for this tool"));
            }

            // Check for dangerous operations
            if (tool.IsModifying)
            {
                warnings.Add("This operation will modify system state");
                
                var userSettings = GetUserSettings(userId);
                if (userSettings.RequireApprovalForModifying && tool.RequiresApproval)
                {
                    requiredActions.Add("User approval required for modifying operation");
                }
            }

            // Validate parameters for security issues
            var securityIssues = ValidateParametersForSecurity(parameters);
            if (securityIssues.Any())
            {
                return Task.FromResult(SecurityCheckResult.Deny($"Security validation failed: {string.Join(", ", securityIssues)}"));
            }

            return Task.FromResult(SecurityCheckResult.Allow(warnings));
        }

        public Task<IEnumerable<ToolExecutionAuditEntry>> GetAuditTrailAsync(string? userId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            lock (_lock)
            {
                var filtered = _auditTrail.AsEnumerable();

                if (!string.IsNullOrEmpty(userId))
                    filtered = filtered.Where(e => e.UserId == userId);

                if (fromDate.HasValue)
                    filtered = filtered.Where(e => e.ExecutionTime >= fromDate.Value);

                if (toDate.HasValue)
                    filtered = filtered.Where(e => e.ExecutionTime <= toDate.Value);

                return Task.FromResult<IEnumerable<ToolExecutionAuditEntry>>(filtered.OrderByDescending(e => e.ExecutionTime));
            }
        }

        private UserPermissionSettings GetUserSettings(string userId)
        {
            lock (_lock)
            {
                if (!_userSettings.TryGetValue(userId, out var settings))
                {
                    settings = new UserPermissionSettings
                    {
                        UserId = userId,
                        PermissionLevel = ToolPermissionLevel.User // Default to user level
                    };
                    
                    // Set default categories for user level
                    settings.AllowedCategories.Add(ToolCategory.Information);
                    settings.AllowedCategories.Add(ToolCategory.FileSystem);
                    settings.AllowedCategories.Add(ToolCategory.Network);
                    
                    _userSettings[userId] = settings;
                }

                return settings;
            }
        }

        private string GenerateApprovalKey(string userId, string toolName, Dictionary<string, object> parameters)
        {
            // Create a key that identifies similar approval requests
            var paramHash = string.Join(",", parameters.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
            return $"{userId}:{toolName}:{paramHash.GetHashCode()}";
        }

        private Task<ToolApprovalResult> SimulateUserApprovalAsync(string userId, ITool tool, Dictionary<string, object> parameters)
        {
            // This is a placeholder - in real implementation, this would show approval UI
            var approvalId = Guid.NewGuid().ToString();
            
            // For now, automatically approve non-modifying tools and require explicit approval for modifying ones
            var isApproved = !tool.IsModifying;
            
            return Task.FromResult(isApproved 
                ? ToolApprovalResult.Approved(approvalId, "Auto-approved (non-modifying)", TimeSpan.FromHours(1))
                : ToolApprovalResult.Denied(approvalId, "Approval required for modifying operations"));
        }

        private List<string> ValidateParametersForSecurity(Dictionary<string, object> parameters)
        {
            var issues = new List<string>();

            foreach (var param in parameters)
            {
                var value = param.Value?.ToString();
                if (string.IsNullOrEmpty(value))
                    continue;

                // Check for common injection patterns
                if (value.Contains("..") || value.Contains("\\\\") || value.Contains("//"))
                    issues.Add($"Suspicious path traversal pattern in parameter '{param.Key}'");

                if (value.Contains(";") || value.Contains("|") || value.Contains("&"))
                    issues.Add($"Potential command injection pattern in parameter '{param.Key}'");

                // Check for script injection
                if (value.Contains("<script") || value.Contains("javascript:"))
                    issues.Add($"Potential script injection in parameter '{param.Key}'");
            }

            return issues;
        }
    }
}
