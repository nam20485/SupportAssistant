using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Security
{
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
}
