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
    /// Complete response from the agent including tool executions
    /// </summary>
    public class AgentResponse
    {
        /// <summary>
        /// Final response text to show the user
        /// </summary>
        public required string ResponseText { get; set; }

        /// <summary>
        /// Tool calls that were executed
        /// </summary>
        public List<ToolExecutionResult> ToolExecutions { get; set; } = new();

        /// <summary>
        /// Whether the response is complete or requires follow-up
        /// </summary>
        public bool IsComplete { get; set; } = true;

        /// <summary>
        /// Any errors that occurred during processing
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Warnings to display to the user
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Total processing time
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
    }
}
