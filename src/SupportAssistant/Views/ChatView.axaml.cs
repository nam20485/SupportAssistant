using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SupportAssistant.ViewModels;

namespace SupportAssistant.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? ViewModel => DataContext as ChatViewModel;

    public ChatView()
    {
        InitializeComponent();
        
        // Subscribe to DataContext changes to wire up events
        DataContextChanged += OnDataContextChanged;
        
        // Focus input when view loads
        Loaded += (_, _) => InputTextBox.Focus();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            // Wire up view model events
            ViewModel.ScrollToBottom += ScrollToBottomImpl;
            ViewModel.CopyToClipboard += CopyToClipboardImpl;
        }
    }

    private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        // Handle keyboard shortcuts
        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Shift+Enter: Allow new line (default behavior)
                return;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+Enter: Send message
                e.Handled = true;
                if (ViewModel.SendMessageCommand.CanExecute(null))
                {
                    ViewModel.SendMessageCommand.Execute(null);
                }
            }
            else
            {
                // Enter alone: Send message
                e.Handled = true;
                if (ViewModel.SendMessageCommand.CanExecute(null))
                {
                    ViewModel.SendMessageCommand.Execute(null);
                }
            }
        }
        else if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Ctrl+L: Clear chat
            e.Handled = true;
            ViewModel.HandleKeyboardShortcut("ctrl+l");
        }
        else if (e.Key == Key.Escape)
        {
            // Escape: Cancel processing
            e.Handled = true;
            ViewModel.HandleKeyboardShortcut("escape");
        }
    }

    private void ScrollToBottomImpl()
    {
        // Use dispatcher to ensure UI updates are complete
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                MessagesScrollViewer.ScrollToEnd();
            }
            catch
            {
                // Ignore scrolling errors
            }
        }, DispatcherPriority.Background);
    }

    private void CopyToClipboardImpl(string content)
    {
        try
        {
            // Use Avalonia's clipboard API
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                clipboard.SetTextAsync(content);
            }
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Clean up event subscriptions
        if (ViewModel != null)
        {
            ViewModel.ScrollToBottom -= ScrollToBottomImpl;
            ViewModel.CopyToClipboard -= CopyToClipboardImpl;
        }
        
        base.OnDetachedFromVisualTree(e);
    }
}
