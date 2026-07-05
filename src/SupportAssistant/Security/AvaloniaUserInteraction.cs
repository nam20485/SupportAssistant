using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SupportAssistant.Core.Security;
using SupportAssistant.Core.Tools;

namespace SupportAssistant.Security
{
    /// <summary>
    /// <see cref="IUserInteraction"/> implementation that shows a modal <see cref="ToolApprovalDialog"/>
    /// on the main window (Phase 4 T3.2). The owner window is resolved lazily at request time (the
    /// main window is created after onboarding completes), and the dialog is shown on the UI thread
    /// so approval requests originating from background agent work still marshal correctly.
    /// </summary>
    public class AvaloniaUserInteraction : IUserInteraction
    {
        private readonly Func<Window?> _ownerProvider;

        public AvaloniaUserInteraction(Func<Window?> ownerProvider)
        {
            _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        }

        /// <summary>Convenience factory that resolves the owner from the running desktop application.</summary>
        public static AvaloniaUserInteraction FromApplication() =>
            new(() => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        public async Task<ToolApprovalResult> RequestApprovalAsync(
            ITool tool,
            Dictionary<string, object> parameters,
            string preview,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var owner = _ownerProvider()
                ?? throw new InvalidOperationException("No main window is available to show the approval dialog.");

            var dialog = new ToolApprovalDialog(tool.Name, preview);
            await Dispatcher.UIThread.InvokeAsync(async () => await dialog.ShowDialog<bool>(owner), DispatcherPriority.Normal).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            return dialog.Approved
                ? ToolApprovalResult.Approved(
                    Guid.NewGuid().ToString(),
                    "Approved by user",
                    validity: dialog.Remember ? TimeSpan.FromHours(1) : null,
                    remember: dialog.Remember)
                : ToolApprovalResult.Denied(Guid.NewGuid().ToString(), "Denied by user");
        }
    }
}
