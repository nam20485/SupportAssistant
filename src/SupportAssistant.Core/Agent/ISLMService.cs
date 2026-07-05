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
}
