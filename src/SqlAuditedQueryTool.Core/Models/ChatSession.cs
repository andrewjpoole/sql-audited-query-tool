namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents a chat conversation session with the LLM.
/// </summary>
public sealed class ChatSession
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastMessageAt { get; init; }
    public List<ChatMessageHistory> Messages { get; init; } = [];
}

/// <summary>
/// Represents a single message in a chat session.
/// </summary>
public sealed class ChatMessageHistory
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required string Role { get; init; } // "user", "assistant", "tool"
    public required string Content { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
}
