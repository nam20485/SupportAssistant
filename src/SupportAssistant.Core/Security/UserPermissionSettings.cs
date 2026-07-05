using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Security
{
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
}
