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
    /// Interface for SLM service integration
    /// </summary>
    public interface ISLMService
    {
        /// <summary>
        /// Generate a response from the SLM for the given prompt
        /// </summary>
        Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the SLM service is available
        /// </summary>
        bool IsAvailable { get; }
    }

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

    /// <summary>
    /// Default implementation of agent orchestrator
    /// </summary>
    public class AgentOrchestrator : IAgentOrchestrator
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly ISecurityManager _securityManager;
        private ISLMService? _slmService;

        // Regex patterns for parsing tool calls from SLM output
        private static readonly Regex ToolCallPattern = new(
            @"<tool_call>\s*(?<json>\{[^}]*(?:\{[^}]*\}[^}]*)*\})\s*</tool_call>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        private static readonly Regex JsonToolCallPattern = new(
            @"```json\s*(?<json>\{[^}]*""tool_name""[^}]*\})\s*```",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        public AgentOrchestrator(IToolRegistry toolRegistry, ISecurityManager securityManager)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _securityManager = securityManager ?? throw new ArgumentNullException(nameof(securityManager));
        }

        public void RegisterSLMService(ISLMService slmService)
        {
            _slmService = slmService ?? throw new ArgumentNullException(nameof(slmService));
        }

        public async Task<AgentResponse> ProcessQueryAsync(string userId, string query, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var response = new AgentResponse { ResponseText = "" };

            try
            {
                // Step 1: Generate initial response with tool descriptions
                var toolsPrompt = GetAvailableToolsPrompt(userId);
                var initialPrompt = BuildInitialPrompt(query, toolsPrompt);
                
                // This would call the SLM - for now we'll simulate
                var slmResponse = await SimulateSLMResponseAsync(initialPrompt, cancellationToken);

                // Step 2: Parse tool calls from the response
                var toolCalls = ParseToolCalls(slmResponse);

                // Step 3: Execute tool calls
                foreach (var toolCall in toolCalls)
                {
                    var executionResult = await ExecuteToolCallAsync(userId, toolCall, cancellationToken);
                    response.ToolExecutions.Add(executionResult);

                    if (!executionResult.ToolResult.IsSuccess)
                    {
                        response.Errors.Add($"Tool {toolCall.ToolName} failed: {executionResult.ToolResult.ErrorMessage}");
                    }
                }

                // Step 4: Generate final response incorporating tool results
                if (response.ToolExecutions.Any())
                {
                    response.ResponseText = await GenerateFollowUpResponseAsync(query, response.ToolExecutions, cancellationToken);
                }
                else
                {
                    response.ResponseText = ExtractNonToolResponse(slmResponse);
                }

                response.ProcessingTime = DateTime.UtcNow - startTime;
                return response;
            }
            catch (Exception ex)
            {
                response.Errors.Add($"Unexpected error: {ex.Message}");
                response.ResponseText = "I apologize, but I encountered an error processing your request. Please try again.";
                response.ProcessingTime = DateTime.UtcNow - startTime;
                return response;
            }
        }

        public string GetAvailableToolsPrompt(string userId)
        {
            var userPermissionLevel = _securityManager.GetUserPermissionLevel(userId);
            var availableTools = _toolRegistry.GetAuthorizedTools(userPermissionLevel);

            if (!availableTools.Any())
            {
                return "No tools are currently available for your permission level.";
            }

            var prompt = "## Available Tools\n\n";
            prompt += "You can use the following tools to help answer the user's question. ";
            prompt += "To use a tool, format your response with <tool_call> tags containing JSON with the tool name and parameters.\n\n";

            foreach (var categoryGroup in availableTools.GroupBy(t => t.Category))
            {
                prompt += $"### {categoryGroup.Key} Tools\n";
                
                foreach (var tool in categoryGroup.OrderBy(t => t.Name))
                {
                    var approvalNote = tool.RequiresApproval ? " (requires user approval)" : "";
                    var modifyingNote = tool.IsModifying ? " [MODIFIES SYSTEM]" : " [READ-ONLY]";
                    
                    prompt += $"- **{tool.Name}**{modifyingNote}{approvalNote}: {tool.Description}\n";
                    prompt += $"  Parameters: {tool.ParameterSchema}\n\n";
                }
            }

            prompt += "### Tool Call Format\n";
            prompt += "To call a tool, use this exact format:\n";
            prompt += "```\n";
            prompt += "<tool_call>\n";
            prompt += "{\n";
            prompt += "  \"tool_name\": \"ToolName\",\n";
            prompt += "  \"parameters\": {\n";
            prompt += "    \"param1\": \"value1\",\n";
            prompt += "    \"param2\": \"value2\"\n";
            prompt += "  },\n";
            prompt += "  \"reasoning\": \"Why this tool is needed\"\n";
            prompt += "}\n";
            prompt += "</tool_call>\n";
            prompt += "```\n\n";

            return prompt;
        }

        public List<ToolCall> ParseToolCalls(string slmResponse)
        {
            var toolCalls = new List<ToolCall>();

            // Try XML-style tool calls first
            var xmlMatches = ToolCallPattern.Matches(slmResponse);
            foreach (Match match in xmlMatches)
            {
                var jsonContent = match.Groups["json"].Value;
                var toolCall = ParseSingleToolCall(jsonContent);
                if (toolCall != null)
                {
                    toolCalls.Add(toolCall);
                }
            }

            // If no XML-style matches, try JSON code blocks
            if (toolCalls.Count == 0)
            {
                var jsonMatches = JsonToolCallPattern.Matches(slmResponse);
                foreach (Match match in jsonMatches)
                {
                    var jsonContent = match.Groups["json"].Value;
                    var toolCall = ParseSingleToolCall(jsonContent);
                    if (toolCall != null)
                    {
                        toolCalls.Add(toolCall);
                    }
                }
            }

            return toolCalls;
        }

        public async Task<ToolExecutionResult> ExecuteToolCallAsync(string userId, ToolCall toolCall, CancellationToken cancellationToken = default)
        {
            var result = new ToolExecutionResult
            {
                ToolCall = toolCall,
                ToolResult = ToolResult.Failure("Tool not found"),
                RequiredApproval = false
            };

            try
            {
                // Get the tool
                var tool = _toolRegistry.GetTool(toolCall.ToolName);
                if (tool == null)
                {
                    result.ToolResult = ToolResult.Failure($"Unknown tool: {toolCall.ToolName}");
                    return result;
                }

                // Validate parameters
                var paramValidation = tool.ValidateParameters(toolCall.Parameters);
                if (!paramValidation.IsValid)
                {
                    result.ToolResult = ToolResult.Failure($"Parameter validation failed: {string.Join(", ", paramValidation.Errors)}");
                    return result;
                }

                // Security check
                var securityCheck = await _securityManager.ValidateExecutionAsync(userId, tool, toolCall.Parameters);
                result.SecurityResult = securityCheck;
                
                if (!securityCheck.IsAllowed)
                {
                    result.ToolResult = ToolResult.Failure($"Security check failed: {securityCheck.DenialReason}");
                    return result;
                }

                // Check if approval is required
                ToolApprovalResult? approvalResult = null;
                if (tool.RequiresApproval)
                {
                    result.RequiredApproval = true;
                    approvalResult = await _securityManager.RequestApprovalAsync(userId, tool, toolCall.Parameters);
                    result.ApprovalResult = approvalResult;

                    if (!approvalResult.IsApproved)
                    {
                        result.ToolResult = ToolResult.Failure($"User approval denied: {approvalResult.UserComments}");
                        return result;
                    }
                }

                // Create backup if needed
                ToolBackupInfo? backupInfo = null;
                if (tool.IsModifying)
                {
                    backupInfo = await _securityManager.CreateBackupAsync(tool, toolCall.Parameters);
                }

                // Execute the tool
                var executionContext = new ToolExecutionContext(
                    toolCall.Parameters,
                    userId,
                    toolCall.Id,
                    approvalResult?.IsApproved ?? false
                );

                result.ToolResult = await tool.ExecuteAsync(executionContext, cancellationToken);

                // Log execution
                var auditEntry = new ToolExecutionAuditEntry(
                    Guid.NewGuid().ToString(),
                    userId,
                    tool.Name,
                    toolCall.Parameters,
                    result.ToolResult.IsSuccess ? "Success" : result.ToolResult.ErrorMessage ?? "Failed",
                    result.ToolResult.IsSuccess,
                    DateTime.UtcNow,
                    result.ToolResult.ExecutionTime,
                    result.ToolResult.ModifiedFiles,
                    backupInfo?.BackupId,
                    Environment.MachineName,
                    tool.RequiresApproval,
                    approvalResult?.ApprovalId
                );

                await _securityManager.LogExecutionAsync(auditEntry);

                return result;
            }
            catch (Exception ex)
            {
                result.ToolResult = ToolResult.Failure($"Execution error: {ex.Message}", ex);
                return result;
            }
        }

        public async Task<string> GenerateFollowUpResponseAsync(string originalQuery, List<ToolExecutionResult> toolResults, CancellationToken cancellationToken = default)
        {
            var prompt = BuildFollowUpPrompt(originalQuery, toolResults);
            
            // This would call the SLM - for now we'll simulate
            return await SimulateFollowUpResponseAsync(prompt, toolResults, cancellationToken);
        }

        public async Task<AgentResponse> ExecuteReActCycleAsync(string userId, string query, int maxIterations = 5, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var response = new AgentResponse { ResponseText = "" };
            var iterationHistory = new List<string>();
            var reasoning = new List<string>();

            try
            {
                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                    // Reasoning phase - generate SLM response with tool awareness
                    var iterationPrompt = BuildReActPrompt(query, iterationHistory, iteration, userId);
                    var slmResponse = _slmService != null && _slmService.IsAvailable
                        ? await _slmService.GenerateResponseAsync(iterationPrompt, cancellationToken)
                        : await SimulateSLMResponseAsync(iterationPrompt, cancellationToken);

                    iterationHistory.Add(slmResponse);
                    reasoning.Add(ExtractReasoning(slmResponse));

                    // Acting phase - parse and execute tool calls
                    var toolCalls = ParseToolCalls(slmResponse);
                    if (!toolCalls.Any())
                    {
                        // No tools to execute, extract final response and finish
                        response.ResponseText = ExtractNonToolResponse(slmResponse);
                        break;
                    }

                    // Execute all tool calls for this iteration
                    var iterationExecutions = new List<ToolExecutionResult>();
                    foreach (var toolCall in toolCalls)
                    {
                        var executionResult = await ExecuteToolCallAsync(userId, toolCall, cancellationToken);
                        iterationExecutions.Add(executionResult);
                        response.ToolExecutions.Add(executionResult);

                        if (!executionResult.ToolResult.IsSuccess)
                        {
                            response.Errors.Add($"Tool {toolCall.ToolName} failed: {executionResult.ToolResult.ErrorMessage}");
                        }
                    }

                    // Observing phase - check if we should continue based on results
                    if (ShouldStopReActCycle(slmResponse, iterationExecutions))
                    {
                        // Generate final response incorporating all tool results
                        response.ResponseText = await GenerateFollowUpResponseAsync(query, response.ToolExecutions, cancellationToken);
                        break;
                    }
                }

                // If we've completed all iterations without a final response, generate one
                if (string.IsNullOrEmpty(response.ResponseText))
                {
                    response.ResponseText = await GenerateFollowUpResponseAsync(query, response.ToolExecutions, cancellationToken);
                }

                response.IsComplete = !response.Errors.Any() && response.ToolExecutions.All(t => t.ToolResult.IsSuccess);
                response.ProcessingTime = DateTime.UtcNow - startTime;

                return response;
            }
            catch (Exception ex)
            {
                response.Errors.Add($"ReAct cycle error: {ex.Message}");
                response.ResponseText = "I encountered an error during the reasoning cycle. Please try again.";
                response.ProcessingTime = DateTime.UtcNow - startTime;
                return response;
            }
        }

        private static ToolCall? ParseSingleToolCall(string jsonContent)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                if (!root.TryGetProperty("tool_name", out var toolNameElement))
                    return null;

                var toolCall = new ToolCall
                {
                    ToolName = toolNameElement.GetString() ?? "",
                    Parameters = new Dictionary<string, object>()
                };

                if (root.TryGetProperty("parameters", out var parametersElement))
                {
                    foreach (var parameter in parametersElement.EnumerateObject())
                    {
                        toolCall.Parameters[parameter.Name] = JsonElementToObject(parameter.Value);
                    }
                }

                if (root.TryGetProperty("reasoning", out var reasoningElement))
                {
                    toolCall.Reasoning = reasoningElement.GetString();
                }

                if (root.TryGetProperty("expected_output", out var expectedElement))
                {
                    toolCall.ExpectedOutput = expectedElement.GetString();
                }

                return toolCall;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => "",
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
                _ => element.ToString()
            };
        }

        private static string BuildInitialPrompt(string userQuery, string toolsPrompt)
        {
            return $@"You are an AI assistant with access to system tools. 

{toolsPrompt}

User Query: {userQuery}

Please analyze the user's query and determine if any tools would be helpful to provide a complete answer. If so, use the appropriate tools and then provide a comprehensive response based on the results.

If you need to use tools, call them first, then provide your analysis and response based on the tool results.";
        }

        private static string BuildFollowUpPrompt(string originalQuery, List<ToolExecutionResult> toolResults)
        {
            var prompt = $@"User Query: {originalQuery}

Tool Execution Results:
";

            foreach (var result in toolResults)
            {
                prompt += $@"
Tool: {result.ToolCall.ToolName}
Parameters: {JsonSerializer.Serialize(result.ToolCall.Parameters)}
Result: {(result.ToolResult.IsSuccess ? "Success" : "Failed")}
";
                if (result.ToolResult.IsSuccess)
                {
                    prompt += $"Data: {JsonSerializer.Serialize(result.ToolResult.Data)}\n";
                    prompt += $"Message: {result.ToolResult.Message}\n";
                }
                else
                {
                    prompt += $"Error: {result.ToolResult.ErrorMessage}\n";
                }
            }

            prompt += @"

Based on the tool execution results above, provide a comprehensive response to the user's original query. 
Incorporate the data from successful tool executions and explain any failures clearly.
Be helpful, accurate, and informative.";

            return prompt;
        }

        private static string ExtractNonToolResponse(string slmResponse)
        {
            // Remove tool call sections from the response
            var cleanResponse = ToolCallPattern.Replace(slmResponse, "");
            cleanResponse = JsonToolCallPattern.Replace(cleanResponse, "");
            
            return cleanResponse.Trim();
        }

        private async Task<string> SimulateSLMResponseAsync(string prompt, CancellationToken cancellationToken)
        {
            // This is a placeholder for actual SLM integration
            // In the real implementation, this would call the ONNX Runtime with the Phi-3 model
            await Task.Delay(100, cancellationToken); // Simulate processing time
            
            // Detect if this looks like a file-related query
            if (prompt.ToLowerInvariant().Contains("read") && 
                (prompt.ToLowerInvariant().Contains("file") || prompt.ToLowerInvariant().Contains(".txt") || prompt.ToLowerInvariant().Contains(".log")))
            {
                return @"I can help you read that file. Let me use the ReadFileContents tool to retrieve the file contents.

<tool_call>
{
  ""tool_name"": ""ReadFileContents"",
  ""parameters"": {
    ""filePath"": ""example.txt"",
    ""maxLines"": 100,
    ""encoding"": ""UTF-8""
  },
  ""reasoning"": ""User wants to read file contents, using ReadFileContents tool to safely retrieve the data""
}
</tool_call>

Based on the file contents, I can provide you with the information you're looking for.";
            }

            return "I understand your query. Let me help you with that information.";
        }

        private async Task<string> SimulateFollowUpResponseAsync(string prompt, List<ToolExecutionResult> toolResults, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken); // Simulate processing time

            var response = "Based on the tool execution results:\n\n";

            foreach (var result in toolResults)
            {
                if (result.ToolResult.IsSuccess)
                {
                    response += $"✅ Successfully executed {result.ToolCall.ToolName}\n";
                    if (result.ToolResult.Message != null)
                    {
                        response += $"   {result.ToolResult.Message}\n";
                    }
                    if (result.ToolResult.Data != null)
                    {
                        response += $"   Result data is available for analysis.\n";
                    }
                }
                else
                {
                    response += $"❌ Failed to execute {result.ToolCall.ToolName}: {result.ToolResult.ErrorMessage}\n";
                }
            }

            response += "\nI hope this information helps answer your question. Let me know if you need any clarification or have additional questions!";

            return response;
        }

        private static string BuildReActPrompt(string originalQuery, List<string> iterationHistory, int currentIteration, string userId)
        {
            var prompt = $@"You are in iteration {currentIteration + 1} of a ReAct (Reasoning, Acting, Observing) cycle.

Original User Query: {originalQuery}

Previous Iterations:
{string.Join("\n---ITERATION---\n", iterationHistory.Select((iter, i) => $"Iteration {i + 1}:\n{iter}"))}

Continue the reasoning process. If you need more information or actions, use the available tools. If you have enough information to provide a complete answer, explain your conclusion without using more tools.

Guidelines:
1. Reason about what you know so far
2. Identify what information is still needed
3. Use tools to gather missing information
4. Observe the results and continue reasoning
5. Provide a final answer when you have sufficient information

Remember to format tool calls correctly and explain your reasoning clearly.";

            return prompt;
        }

        private static string ExtractReasoning(string slmResponse)
        {
            // Look for reasoning sections in the response
            var patterns = new[]
            {
                @"## Reasoning\s*\n(.*?)(?=\n##|\n<tool_call>|$)",
                @"## Analysis\s*\n(.*?)(?=\n##|\n<tool_call>|$)",
                @"Let me think about this\.\s*(.*?)(?=\n<tool_call>|$)",
                @"I need to\s*(.*?)(?=\n<tool_call>|$)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(slmResponse, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            // If no explicit reasoning section, extract first paragraph
            var lines = slmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Any())
            {
                return lines[0].Trim();
            }

            return "Analyzing the query...";
        }

        private static bool ShouldStopReActCycle(string slmResponse, List<ToolExecutionResult> iterationExecutions)
        {
            // Stop if the response indicates completion
            var completionIndicators = new[]
            {
                "I have completed",
                "Task is complete",
                "No further actions needed",
                "That concludes",
                "Final answer:",
                "In conclusion",
                "To summarize"
            };

            var hasCompletionIndicator = completionIndicators.Any(indicator => 
                slmResponse.Contains(indicator, StringComparison.OrdinalIgnoreCase));

            // Stop if any tool execution failed critically
            var hasCriticalFailure = iterationExecutions.Any(e => 
                !e.ToolResult.IsSuccess && 
                (e.ToolResult.ErrorMessage?.Contains("Access denied") == true ||
                 e.ToolResult.ErrorMessage?.Contains("Security check failed") == true));

            return hasCompletionIndicator || hasCriticalFailure;
        }
    }
}
