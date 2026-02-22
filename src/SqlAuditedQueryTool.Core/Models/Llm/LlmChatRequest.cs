namespace SqlAuditedQueryTool.Core.Models.Llm;

public sealed class LlmChatRequest
{
    public string? SystemPrompt { get; init; }
    public required List<ChatMessage> Messages { get; init; }
    public SchemaContext? SchemaContext { get; init; }
}

public sealed class ChatMessage
{
    public required string Role { get; init; } // "user", "assistant", "system"
    public required string Content { get; init; }
}
