using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Security
{
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
