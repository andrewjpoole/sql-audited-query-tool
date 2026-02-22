using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Core.Interfaces;

public interface IQueryExecutor
{
    Task<QueryResult> ExecuteReadOnlyQueryAsync(QueryRequest request);
}
