using System;

namespace SupportAssistant.Models;

public class ChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Content { get; init; } = string.Empty;
    public ChatMessageType Type { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsFromUser => Type == ChatMessageType.User;
    public bool IsFromAssistant => Type == ChatMessageType.Assistant;
    public bool IsSystemMessage => Type == ChatMessageType.System;
}

public enum ChatMessageType
{
    User,
    Assistant,
    System
}
