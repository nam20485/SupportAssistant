# Plan: Example Prompts on Empty Chat

## Goal
When the chat has no user/assistant conversation yet (only System messages), show two clickable example prompts above the message list footer with a short hint label. Clicking an example fills the send box (and focuses it) — it does NOT auto-send.

## Confirmed Decisions
1. **Click behavior:** Fill the send box + focus the input. **No auto-send.** User presses Enter/Send.
2. **When visible:** Whenever no `User` or `Assistant` messages exist — i.e. on app start AND after `ClearChat` (both leave only a System message). Hidden as soon as a user message is added.
3. **Examples (hard-coded defaults):**
   - `How do I change my desktop background?`
   - `How do I take a screenshot?`

## Affected Files
- `src/SupportAssistant/ViewModels/ChatViewModel.cs` — add prompts data, command, visibility property, recompute hook, focus event.
- `src/SupportAssistant/Views/ChatView.axaml` — render hint + clickable examples in the messages area.
- `src/SupportAssistant/Views/ChatView.axaml.cs` — wire the new focus event to `InputTextBox.Focus()`.
- `src/SupportAssistant.Tests/UI/Phase34AdvancedUITests.cs` (or a new `ExamplePromptsTests.cs`) — add tests.

## Implementation Tasks

### 1. ChatViewModel.cs
- Add a backing field `bool _showExamplePrompts` and public property `ShowExamplePrompts` using `RaiseAndSetIfChanged`.
- Add `public IReadOnlyList<string> ExamplePrompts { get; }` initialized to the two confirmed strings.
- Add a computed method `private void RefreshExamplePromptsVisibility()` that sets `ShowExamplePrompts = !Messages.Any(m => m.Type == ChatMessageType.User || m.Type == ChatMessageType.Assistant);`.
- Call `RefreshExamplePromptsVisibility()`:
  - At end of constructor (after the welcome message is added).
  - Inside `OnMessagesCollectionChanged` for `Add`/`Remove`/`Reset` actions (so it updates on send and on `ClearChat`).
- Add `UseExamplePromptCommand = ReactiveCommand.Create<string>(UseExamplePrompt);` where:
  ```csharp
  private void UseExamplePrompt(string prompt)
  {
      if (string.IsNullOrWhiteSpace(prompt)) return;
      UserInput = prompt;
      FocusInputRequested?.Invoke();
  }
  ```
- Add event `public event Action? FocusInputRequested;` (mirrors the existing `ScrollToBottom` event pattern).

### 2. ChatView.axaml
- Inside the messages `ScrollViewer` → `StackPanel`, after the messages `ItemsControl` and BEFORE the typing-indicator `Border`, insert a new panel bound to `ShowExamplePrompts`:
  - A `Border` (or `StackPanel`) with `IsVisible="{Binding ShowExamplePrompts}"`, centered, e.g. `HorizontalAlignment="Center"`, `Margin="0,15,0,5"`.
  - Hint label `TextBlock`: `Text="💡 Try one of these examples:"` (or "Click an example to try it"), styled with `AppMutedForeground`, `FontStyle="Italic"`, `FontSize="12"`.
  - An `ItemsControl` `ItemsSource="{Binding ExamplePrompts}"` rendering each prompt as a clickable button (looks like a chip/card):
    - Use `ItemsControl.ItemsPanel` = `StackPanel Orientation="Horizontal"` (or WrapPanel if available) for side-by-side chips.
    - `ItemTemplate`: a `Button` with `Content="{Binding}"`, `Command="{Binding $parent[UserControl].((vm:ChatViewModel)DataContext).UseExamplePromptCommand}"`, `CommandParameter="{Binding}"`, `Cursor="Hand"`, `Padding="12,6"`, `Margin="5"`, background `AppSubtleBackground`, `CornerRadius` via wrapping `Border`, `ToolTip.Tip="Click to use this example"`.
  - Reuse existing theme brushes (`AppSubtleBackground`, `AppBorderBrush`, `AppMutedForeground`, `AppStrongForeground`) for light/dark consistency.

### 3. ChatView.axaml.cs
- In `OnDataContextChanged`, subscribe: `ViewModel.FocusInputRequested += FocusInputImpl;`
- Add:
  ```csharp
  private void FocusInputImpl()
  {
      Dispatcher.UIThread.Post(() => InputTextBox.Focus(), DispatcherPriority.Background);
  }
  ```
- In `OnDetachedFromVisualTree`, unsubscribe `ViewModel.FocusInputRequested -= FocusInputImpl;` (guard null ViewModel).

### 4. Tests
Add to `Phase34AdvancedUITests.cs` (or new `ExamplePromptsTests.cs`):
- `ShowExamplePrompts` is `true` on a fresh ViewModel (only welcome System message).
- `ExamplePrompts` contains exactly the two confirmed strings.
- Adding a `User` message sets `ShowExamplePrompts = false`.
- Adding only an `Assistant` message sets `ShowExamplePrompts = false`.
- `ClearChatCommand` restores `ShowExamplePrompts = true`.
- `UseExamplePromptCommand.Execute("X")` sets `UserInput = "X"`.
- `UseExamplePromptCommand.Execute(null/"")` leaves `UserInput` unchanged.
- `UseExamplePromptCommand` raises `FocusInputRequested` (subscribe a flag, assert true).

## Validation
- `dotnet build SupportAssistant.sln` succeeds.
- `dotnet test` (the SupportAssistant.Tests project) — all existing + new tests pass.
- Manual/Avalonia visual check: examples appear on launch and after Clear; disappear after sending; clicking fills + focuses the box without sending.

## Risks / Notes
- `$parent[UserControl]` binding in the example `Button` is already used for `CopyMessageCommand`, so the pattern is proven in this XAML.
- `Button` text uses `Content="{Binding}"` where the DataContext of each item is the string itself — fine for a plain string list.
- No localization system exists, so label/example strings are hard-coded (consistent with existing welcome message).
- This change is non-breaking; existing commands/tests are untouched.
