using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Core.Interfaces;

public interface IQueryHistoryStore
{
    Task<QueryHistory> AddAsync(QueryHistory entry);
    Task<IReadOnlyList<QueryHistory>> GetAllAsync(int limit = 100);
    Task<QueryHistory?> GetByIdAsync(Guid id);
}
