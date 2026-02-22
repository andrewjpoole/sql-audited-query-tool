using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Core.Interfaces;

public interface IAuditLogger
{
    Task<AuditEntry> LogQueryAsync(QueryRequest request, QueryResult result);
}
