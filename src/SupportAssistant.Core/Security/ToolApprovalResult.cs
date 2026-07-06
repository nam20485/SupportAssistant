using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Security
{
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
}
