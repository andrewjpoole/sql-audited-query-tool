namespace SqlAuditedQueryTool.Core.Models.Llm;

public class VectorSearchResult
{
    public required string Key { get; init; }
    public required float[] Embedding { get; init; }
    public required VectorMetadata Metadata { get; init; }
    public required float Score { get; init; }
}
