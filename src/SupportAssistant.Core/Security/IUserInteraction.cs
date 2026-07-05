using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Core.Security
{
    /// <summary>
    /// Human-in-the-loop interaction surface used by <see cref="ISecurityManager"/> to obtain an
    /// explicit user decision before a modifying/approval-requiring tool runs. Implementations live
    /// in the host (Avalonia modal dialog); tests supply a mock.
    /// </summary>
    public interface IUserInteraction
    {
        /// <summary>
        /// Asks the user to approve a tool execution, showing <paramref name="preview"/> (a plain-text
        /// description of the tool, its parameters, and what it will change). Honors
        /// <paramref name="cancellationToken"/> so a cancelled approval flow returns promptly.
        /// </summary>
        Task<ToolApprovalResult> RequestApprovalAsync(
            ITool tool,
            Dictionary<string, object> parameters,
            string preview,
            CancellationToken cancellationToken = default);
    }
}
