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
    /// Represents a tool call parsed from SLM output
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// Unique identifier for this tool call
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of the tool to call
        /// </summary>
        public required string ToolName { get; set; }

        /// <summary>
        /// Parameters for the tool call
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Reasoning for why this tool is being called
        /// </summary>
        public string? Reasoning { get; set; }

        /// <summary>
        /// Expected output or what the tool call should accomplish
        /// </summary>
        public string? ExpectedOutput { get; set; }
    }
}
