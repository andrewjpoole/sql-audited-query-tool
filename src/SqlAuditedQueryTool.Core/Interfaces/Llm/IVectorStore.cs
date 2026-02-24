using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Core.Interfaces.Llm;

public interface IVectorStore
{
    Task UpsertAsync(string key, float[] embedding, VectorMetadata metadata, CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(float[] query, int topK = 10, string? category = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
