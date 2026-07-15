using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SupportAssistant.Core.Engines;

namespace SupportAssistant.Views;

/// <summary>
/// Informs the user that the host is ROCm-compatible but the GPU is a legacy ISA that needs
/// <c>HSA_OVERRIDE_GFX_VERSION</c> at process launch. Continue → CPU fallback; Exit → shut down
/// so the user can set the variable and restart.
/// </summary>
public sealed class LegacyRocmGpuDialog : Window
{
    /// <summary>True when the user checked "Don't show again".</summary>
    public bool DontAskAgain { get; private set; }

    public LegacyRocmGpuDialog()
    {
        // Designer / activator.
    }

    public LegacyRocmGpuDialog(RocmPreflightResult preflight)
    {
        ArgumentNullException.ThrowIfNull(preflight);

        Title = "Legacy AMD GPU detected";
        Width = 560;
        MinHeight = 320;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var archLabel = preflight.LegacyArchNames.Count > 0
            ? string.Join(", ", preflight.LegacyArchNames)
            : "unknown";

        var body = new TextBlock
        {
            Text =
                "This system is supported for ROCm inference, but your GPU uses a legacy architecture " +
                $"({archLabel}).\n\n" +
                "To use ROCm GPU acceleration, exit Support Assistant, set this environment variable " +
                "in the environment that launches the app, then restart:\n\n" +
                $"  export {RocmHostPreflight.OverrideEnvironmentVariable}={RocmHostPreflight.RecommendedOverrideValue}\n\n" +
                "(Shell profiles, IDE launch configs, or desktop entries all work. Setting it after " +
                "the process has started does not work.)\n\n" +
                "If you continue now, inference will fall back to CPU for this session.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 20, 20, 12),
        };

        var dontAsk = new CheckBox
        {
            Content = "Don't show again (always fall back to CPU when this applies)",
            Margin = new Thickness(20, 0, 20, 12),
        };

        var exit = new Button
        {
            Content = "Exit",
            Padding = new Thickness(16, 6),
            Margin = new Thickness(4, 0),
        };
        var continueCpu = new Button
        {
            Content = "Continue with CPU",
            Padding = new Thickness(16, 6),
            Margin = new Thickness(4, 0),
        };

        exit.Click += (_, _) =>
        {
            DontAskAgain = dontAsk.IsChecked == true;
            Close(false);
        };
        continueCpu.Click += (_, _) =>
        {
            DontAskAgain = dontAsk.IsChecked == true;
            Close(true);
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(20, 8, 20, 20),
        };
        buttons.Children.Add(exit);
        buttons.Children.Add(continueCpu);

        var root = new StackPanel();
        root.Children.Add(body);
        root.Children.Add(dontAsk);
        root.Children.Add(buttons);
        Content = root;
    }
}
