using System.Collections.Concurrent;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Database;

/// <summary>
/// In-memory implementation of chat history storage.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class InMemoryChatHistoryStore : IChatHistoryStore
{
    private readonly ConcurrentDictionary<Guid, ChatSession> _sessions = new();
    private readonly ConcurrentQueue<Guid> _insertionOrder = new();

    public Task<ChatSession> CreateSessionAsync(string title)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            LastMessageAt = DateTimeOffset.UtcNow,
            Messages = []
        };

        _sessions[session.Id] = session;
        _insertionOrder.Enqueue(session.Id);
        
        return Task.FromResult(session);
    }

    public Task<ChatSession?> GetSessionAsync(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<ChatSession>> GetAllSessionsAsync(int limit = 50)
    {
        var results = _insertionOrder
            .Reverse()
            .Take(limit)
            .Select(id => _sessions.TryGetValue(id, out var session) ? session : null)
            .Where(s => s is not null)
            .Cast<ChatSession>()
            .ToList();

        return Task.FromResult<IReadOnlyList<ChatSession>>(results);
    }

    public Task<ChatSession> AddMessageAsync(Guid sessionId, ChatMessageHistory message)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Chat session {sessionId} not found");
        }

        // Create updated session with new message
        var updatedSession = new ChatSession
        {
            Id = session.Id,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            LastMessageAt = DateTimeOffset.UtcNow,
            Messages = [.. session.Messages, message]
        };

        _sessions[sessionId] = updatedSession;
        return Task.FromResult(updatedSession);
    }

    public Task DeleteSessionAsync(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
