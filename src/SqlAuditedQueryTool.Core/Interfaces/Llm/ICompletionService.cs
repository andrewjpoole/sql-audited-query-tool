using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Core.Interfaces.Llm;

public interface ICompletionService
{
    Task<IReadOnlyList<CompletionItem>> GetSchemaCompletionsAsync(CompletionContext context, CancellationToken ct = default);
}
