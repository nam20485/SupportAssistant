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
    /// Orchestrates interaction between SLM and tool execution
    /// </summary>
    public interface IAgentOrchestrator
    {
        /// <summary>
        /// Process a user query and execute any necessary tools
        /// </summary>
        Task<AgentResponse> ProcessQueryAsync(string userId, string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available tools for inclusion in SLM prompts
        /// </summary>
        string GetAvailableToolsPrompt(string userId);

        /// <summary>
        /// Parse tool calls from SLM response
        /// </summary>
        List<ToolCall> ParseToolCalls(string slmResponse);

        /// <summary>
        /// Execute a specific tool call
        /// </summary>
        Task<ToolExecutionResult> ExecuteToolCallAsync(string userId, ToolCall toolCall, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate follow-up response incorporating tool results
        /// </summary>
        Task<string> GenerateFollowUpResponseAsync(string originalQuery, List<ToolExecutionResult> toolResults, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a complete ReAct (Reasoning, Acting, Observing) cycle for complex tasks
        /// </summary>
        Task<AgentResponse> ExecuteReActCycleAsync(string userId, string query, int maxIterations = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Register an SLM service for processing prompts
        /// </summary>
        void RegisterSLMService(ISLMService slmService);
    }
}
