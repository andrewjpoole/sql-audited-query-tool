using System.Collections.Concurrent;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Database;

/// <summary>
/// In-memory implementation of query history storage.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class InMemoryQueryHistoryStore : IQueryHistoryStore
{
    private readonly ConcurrentDictionary<Guid, QueryHistory> _history = new();
    private readonly ConcurrentQueue<Guid> _insertionOrder = new();

    public Task<QueryHistory> AddAsync(QueryHistory entry)
    {
        _history[entry.Id] = entry;
        _insertionOrder.Enqueue(entry.Id);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<QueryHistory>> GetAllAsync(int limit = 100)
    {
        var results = _insertionOrder
            .Reverse()
            .Take(limit)
            .Select(id => _history.TryGetValue(id, out var entry) ? entry : null)
            .Where(e => e is not null)
            .Cast<QueryHistory>()
            .ToList();

        return Task.FromResult<IReadOnlyList<QueryHistory>>(results);
    }

    public Task<QueryHistory?> GetByIdAsync(Guid id)
    {
        _history.TryGetValue(id, out var entry);
        return Task.FromResult(entry);
    }
}
