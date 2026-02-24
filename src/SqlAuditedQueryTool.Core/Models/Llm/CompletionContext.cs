namespace SqlAuditedQueryTool.Core.Models.Llm;

public record CompletionContext(
    string Prefix,
    string? Context,
    int CursorLine
);
