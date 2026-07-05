using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SupportAssistant.Security
{
    /// <summary>
    /// Modal human-in-the-loop approval dialog (Phase 4 T3.2). Shows the tool name, a plain-text
    /// preview of what will change, and Approve / Deny buttons with an optional "remember this
    /// decision" checkbox. Built programmatically so it needs no separate .axaml.
    /// </summary>
    public class ToolApprovalDialog : Window
    {
        /// <summary>True when the user clicked Approve.</summary>
        public bool Approved { get; private set; }

        /// <summary>True when the user opted to remember the decision for similar operations.</summary>
        public bool Remember { get; private set; }

        public ToolApprovalDialog()
        {
            // Parameterless ctor used by the Avalonia designer/activator.
        }

        public ToolApprovalDialog(string toolName, string preview)
        {
            Title = $"Approve {toolName}?";
            Width = 520;
            MinHeight = 240;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            var previewBlock = new TextBlock
            {
                Text = preview,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16)
            };

            var remember = new CheckBox
            {
                Content = "Remember this decision for similar operations",
                Margin = new Thickness(16, 0, 16, 8)
            };

            var deny = new Button
            {
                Content = "Deny",
                Padding = new Thickness(16, 6),
                Margin = new Thickness(4, 0)
            };
            var approve = new Button
            {
                Content = "Approve",
                Padding = new Thickness(16, 6),
                Margin = new Thickness(4, 0)
            };

            deny.Click += (_, _) =>
            {
                Approved = false;
                Remember = remember.IsChecked ?? false;
                Close(false);
            };
            approve.Click += (_, _) =>
            {
                Approved = true;
                Remember = remember.IsChecked ?? false;
                Close(true);
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16)
            };
            buttons.Children.Add(deny);
            buttons.Children.Add(approve);

            var root = new StackPanel();
            root.Children.Add(previewBlock);
            root.Children.Add(remember);
            root.Children.Add(buttons);

            Content = root;
        }
    }
}
