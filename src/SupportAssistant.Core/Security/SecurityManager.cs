using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        private readonly Dictionary<string, List<BackupTarget>> _backupTargets = new();
        private readonly List<ToolExecutionAuditEntry> _auditTrail = new();
        private readonly IUserInteraction? _userInteraction;
        private readonly string _backupRoot;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates the security manager. <paramref name="userInteraction"/> wires a real human-in-the-loop
        /// approval surface (Avalonia modal dialog); when null, approval falls back to a deterministic
        /// simulation so headless/test environments still function.
        /// </summary>
        public SecurityManager(IUserInteraction? userInteraction = null)
        {
            _userInteraction = userInteraction;
            _backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SupportAssistant", "Backups");
        }

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

            ToolApprovalResult result;
            if (_userInteraction is not null)
            {
                // Real human-in-the-loop approval (Phase 4 T3.2).
                result = await _userInteraction.RequestApprovalAsync(tool, parameters, BuildApprovalPreview(tool, parameters))
                    .ConfigureAwait(false);
            }
            else
            {
                // Headless/test fallback.
                result = await SimulateUserApprovalAsync(userId, tool, parameters).ConfigureAwait(false);
            }

            // Remember the decision if the user opted in (and the approval is bounded in time).
            if (result.RememberDecision && result.IsApproved && result.ValidityDuration.HasValue)
            {
                lock (_lock)
                {
                    _rememberedApprovals[rememberedKey] = result;
                }
            }

            return result;
        }

        public Task<ToolBackupInfo> CreateBackupAsync(ITool tool, Dictionary<string, object> parameters)
        {
            var backupId = Guid.NewGuid().ToString();
            var backedUpFiles = new List<string>();
            var targets = new List<BackupTarget>();

            try
            {
                var backupDir = Path.Combine(_backupRoot, backupId);
                Directory.CreateDirectory(backupDir);

                // Back up each existing target file referenced by the tool's parameters (Phase 4 T3.3).
                // Non-existent targets are recorded as "new files" so restore can delete them.
                foreach (var path in ExtractTargetFilePaths(parameters))
                {
                    var existed = File.Exists(path);
                    if (existed)
                    {
                        var dest = Path.Combine(backupDir, $"{Guid.NewGuid():N}_{Path.GetFileName(path)}");
                        File.Copy(path, dest, overwrite: true);
                        targets.Add(new BackupTarget(path, dest, ExistedBefore: true));
                        backedUpFiles.Add(path);
                    }
                    else
                    {
                        targets.Add(new BackupTarget(path, BackupPath: null, ExistedBefore: false));
                    }
                }
            }
            catch
            {
                // A failed backup is non-fatal to recording the backup record; restore simply will not
                // have the file to copy back, which the caller logs.
            }

            var backupInfo = new ToolBackupInfo(
                backupId,
                DateTime.UtcNow,
                backedUpFiles: backedUpFiles,
                description: $"Backup before {tool.Name} execution",
                canRestore: true);

            lock (_lock)
            {
                _backups[backupId] = backupInfo;
                _backupTargets[backupId] = targets;
            }

            return Task.FromResult(backupInfo);
        }

        public Task<bool> RestoreBackupAsync(string backupId)
        {
            List<BackupTarget>? targets;
            lock (_lock)
            {
                if (!_backups.TryGetValue(backupId, out var backup) || !backup.CanRestore)
                {
                    return Task.FromResult(false);
                }

                _backupTargets.TryGetValue(backupId, out targets);
            }

            if (targets is null)
            {
                return Task.FromResult(false);
            }

            try
            {
                foreach (var target in targets)
                {
                    if (target.ExistedBefore && target.BackupPath is not null)
                    {
                        // Restore the original contents from the backup copy.
                        File.Copy(target.BackupPath, target.OriginalPath, overwrite: true);
                    }
                    else if (File.Exists(target.OriginalPath))
                    {
                        // The file did not exist before the tool ran; restore = remove it.
                        File.Delete(target.OriginalPath);
                    }
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
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

        /// <summary>
        /// Builds a plain-text preview of a tool execution for the approval dialog.
        /// </summary>
        private static string BuildApprovalPreview(ITool tool, Dictionary<string, object> parameters)
        {
            var lines = new List<string>
            {
                $"Tool: {tool.Name}",
                tool.Description ?? string.Empty,
                tool.IsModifying ? "This operation MODIFIES the system." : "This operation is read-only."
            };

            if (parameters.Count > 0)
            {
                lines.Add("Parameters:");
                foreach (var parameter in parameters.OrderBy(p => p.Key))
                {
                    lines.Add($"  {parameter.Key} = {parameter.Value}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Extracts candidate target file paths from a tool's parameters. Backups apply to file-
        /// modifying tools (e.g. WriteFileContents) whose path is carried in a path-like parameter.
        /// </summary>
        private static IEnumerable<string> ExtractTargetFilePaths(Dictionary<string, object> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.Value is null)
                {
                    continue;
                }

                var key = parameter.Key;
                var value = parameter.Value.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var isPathKey = key.Contains("path", StringComparison.OrdinalIgnoreCase)
                                || key.Contains("file", StringComparison.OrdinalIgnoreCase);
                var looksLikePath = value.Contains(Path.DirectorySeparatorChar)
                                    || value.Contains(Path.AltDirectorySeparatorChar)
                                    || Path.GetExtension(value).Length > 0;

                if (isPathKey || looksLikePath)
                {
                    yield return value;
                }
            }
        }

        /// <summary>Records what was backed up for a single target file so restore can reverse it.</summary>
        private sealed record BackupTarget(string OriginalPath, string? BackupPath, bool ExistedBefore);
    }
}
