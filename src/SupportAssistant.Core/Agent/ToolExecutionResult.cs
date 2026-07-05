using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SupportAssistant.Core.Models;
using SupportAssistant.Core.Tools;
using SupportAssistant.Core.Security;

namespace SupportAssistant.Core.Agent
{
    /// <summary>
    /// Result of executing a tool call
    /// </summary>
    public class ToolExecutionResult
    {
        /// <summary>
        /// The original tool call
        /// </summary>
        public required ToolCall ToolCall { get; set; }

        /// <summary>
        /// Result from tool execution
        /// </summary>
        public required ToolResult ToolResult { get; set; }

        /// <summary>
        /// Whether user approval was required
        /// </summary>
        public bool RequiredApproval { get; set; }

        /// <summary>
        /// Approval result if approval was required
        /// </summary>
        public ToolApprovalResult? ApprovalResult { get; set; }

        /// <summary>
        /// Security validation result
        /// </summary>
        public SecurityCheckResult? SecurityResult { get; set; }
    }
}
