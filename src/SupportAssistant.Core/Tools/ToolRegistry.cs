using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Tools
{
    /// <summary>
    /// Registry for managing and discovering available tools
    /// </summary>
    public interface IToolRegistry
    {
        /// <summary>
        /// Register a tool instance
        /// </summary>
        void RegisterTool(ITool tool);

        /// <summary>
        /// Get a tool by name
        /// </summary>
        ITool? GetTool(string name);

        /// <summary>
        /// Get all available tools
        /// </summary>
        IEnumerable<ITool> GetAllTools();

        /// <summary>
        /// Get tools by category
        /// </summary>
        IEnumerable<ITool> GetToolsByCategory(ToolCategory category);

        /// <summary>
        /// Get tools that match the specified permission level
        /// </summary>
        IEnumerable<ITool> GetToolsByPermissionLevel(ToolPermissionLevel maxPermissionLevel);

        /// <summary>
        /// Search for tools by name or description
        /// </summary>
        IEnumerable<ITool> SearchTools(string query);

        /// <summary>
        /// Check if a tool is registered
        /// </summary>
        bool IsToolRegistered(string name);

        /// <summary>
        /// Unregister a tool
        /// </summary>
        bool UnregisterTool(string name);

        /// <summary>
        /// Get tools suitable for the current user's permission level
        /// </summary>
        IEnumerable<ITool> GetAuthorizedTools(ToolPermissionLevel userPermissionLevel);

        /// <summary>
        /// Generate a JSON schema for all available tools for SLM consumption
        /// </summary>
        string GenerateToolsSchema();
    }

    /// <summary>
    /// Default implementation of the tool registry
    /// </summary>
    public class ToolRegistry : IToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new Dictionary<string, ITool>();
        private readonly object _lock = new object();

        public void RegisterTool(ITool tool)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (string.IsNullOrWhiteSpace(tool.Name))
                throw new ArgumentException("Tool name cannot be null or empty", nameof(tool));

            lock (_lock)
            {
                if (_tools.ContainsKey(tool.Name))
                    throw new InvalidOperationException($"Tool '{tool.Name}' is already registered");

                _tools[tool.Name] = tool;
            }
        }

        public ITool? GetTool(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            lock (_lock)
            {
                _tools.TryGetValue(name, out var tool);
                return tool;
            }
        }

        public IEnumerable<ITool> GetAllTools()
        {
            lock (_lock)
            {
                return _tools.Values.ToList();
            }
        }

        public IEnumerable<ITool> GetToolsByCategory(ToolCategory category)
        {
            lock (_lock)
            {
                return _tools.Values.Where(t => t.Category == category).ToList();
            }
        }

        public IEnumerable<ITool> GetToolsByPermissionLevel(ToolPermissionLevel maxPermissionLevel)
        {
            lock (_lock)
            {
                return _tools.Values.Where(t => t.RequiredPermission <= maxPermissionLevel).ToList();
            }
        }

        public IEnumerable<ITool> SearchTools(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAllTools();

            var queryLower = query.ToLowerInvariant();

            lock (_lock)
            {
                return _tools.Values.Where(t =>
                    t.Name.ToLowerInvariant().Contains(queryLower) ||
                    t.Description.ToLowerInvariant().Contains(queryLower)
                ).ToList();
            }
        }

        public bool IsToolRegistered(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_lock)
            {
                return _tools.ContainsKey(name);
            }
        }

        public bool UnregisterTool(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_lock)
            {
                return _tools.Remove(name);
            }
        }

        public IEnumerable<ITool> GetAuthorizedTools(ToolPermissionLevel userPermissionLevel)
        {
            return GetToolsByPermissionLevel(userPermissionLevel);
        }

        public string GenerateToolsSchema()
        {
            var tools = GetAllTools();
            var schema = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>(),
                    required = new[] { "name", "parameters" }
                },
                tools = tools.Select(tool => new
                {
                    name = tool.Name,
                    description = tool.Description,
                    category = tool.Category.ToString(),
                    permission_level = tool.RequiredPermission.ToString(),
                    requires_approval = tool.RequiresApproval,
                    is_modifying = tool.IsModifying,
                    parameter_schema = tool.ParameterSchema
                }).ToArray()
            };

            return System.Text.Json.JsonSerializer.Serialize(schema, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Automatically discover and register tools from assemblies
        /// </summary>
        public void DiscoverAndRegisterTools()
        {
            var assemblies = new[]
            {
                Assembly.GetExecutingAssembly(),
                Assembly.GetAssembly(typeof(ITool))
            }.Where(a => a != null).Distinct();

            foreach (var assembly in assemblies)
            {
                if (assembly != null)
                    DiscoverToolsInAssembly(assembly);
            }
        }

        /// <summary>
        /// Discover tools in a specific assembly
        /// </summary>
        private void DiscoverToolsInAssembly(Assembly assembly)
        {
            try
            {
                var toolTypes = assembly.GetTypes()
                    .Where(t => typeof(ITool).IsAssignableFrom(t) &&
                               !t.IsInterface &&
                               !t.IsAbstract &&
                               t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var toolType in toolTypes)
                {
                    try
                    {
                        if (Activator.CreateInstance(toolType) is ITool tool)
                        {
                            if (!IsToolRegistered(tool.Name))
                            {
                                RegisterTool(tool);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log tool registration failure but continue with other tools
                        System.Diagnostics.Debug.WriteLine($"Failed to register tool {toolType.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log assembly scanning failure but continue
                System.Diagnostics.Debug.WriteLine($"Failed to scan assembly {assembly.FullName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Register default core tools
        /// </summary>
        public void RegisterCoreTools()
        {
            // These will be implemented in subsequent steps
            // RegisterTool(new ReadFileContents());
            // RegisterTool(new WriteFileContents());
            // RegisterTool(new ListDirectory());
            // RegisterTool(new GetSystemInfo());
            // RegisterTool(new PingHost());
        }

        /// <summary>
        /// Get tools formatted for SLM prompt inclusion
        /// </summary>
        public string GetToolDescriptionsForPrompt()
        {
            var tools = GetAllTools().OrderBy(t => t.Category).ThenBy(t => t.Name);
            var descriptions = new List<string>();

            foreach (var categoryGroup in tools.GroupBy(t => t.Category))
            {
                descriptions.Add($"\n## {categoryGroup.Key} Tools:");
                
                foreach (var tool in categoryGroup)
                {
                    var approvalNote = tool.RequiresApproval ? " (requires user approval)" : "";
                    var modifyingNote = tool.IsModifying ? " [MODIFYING]" : " [READ-ONLY]";
                    
                    descriptions.Add($"- **{tool.Name}**{modifyingNote}{approvalNote}: {tool.Description}");
                    descriptions.Add($"  Parameters: {tool.ParameterSchema}");
                }
            }

            return string.Join("\n", descriptions);
        }

        /// <summary>
        /// Get tool usage statistics
        /// </summary>
        public ToolRegistryStats GetStatistics()
        {
            lock (_lock)
            {
                var tools = _tools.Values.ToList();
                
                return new ToolRegistryStats
                {
                    TotalTools = tools.Count,
                    ToolsByCategory = tools.GroupBy(t => t.Category)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ToolsByPermissionLevel = tools.GroupBy(t => t.RequiredPermission)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ToolsRequiringApproval = tools.Count(t => t.RequiresApproval),
                    ModifyingTools = tools.Count(t => t.IsModifying),
                    ReadOnlyTools = tools.Count(t => !t.IsModifying)
                };
            }
        }
    }

    /// <summary>
    /// Statistics about registered tools
    /// </summary>
    public class ToolRegistryStats
    {
        public int TotalTools { get; set; }
        public Dictionary<ToolCategory, int> ToolsByCategory { get; set; } = new();
        public Dictionary<ToolPermissionLevel, int> ToolsByPermissionLevel { get; set; } = new();
        public int ToolsRequiringApproval { get; set; }
        public int ModifyingTools { get; set; }
        public int ReadOnlyTools { get; set; }
    }
}
