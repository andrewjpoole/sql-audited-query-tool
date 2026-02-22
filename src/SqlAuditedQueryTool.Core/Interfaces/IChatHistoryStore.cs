using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Core.Interfaces;

/// <summary>
/// Stores chat conversation history with the LLM.
/// </summary>
public interface IChatHistoryStore
{
    Task<ChatSession> CreateSessionAsync(string title);
    Task<ChatSession?> GetSessionAsync(Guid sessionId);
    Task<IReadOnlyList<ChatSession>> GetAllSessionsAsync(int limit = 50);
    Task<ChatSession> AddMessageAsync(Guid sessionId, ChatMessageHistory message);
    Task DeleteSessionAsync(Guid sessionId);
}
