namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents the result of generating sql-script-runner scripts.
/// </summary>
public sealed class ScriptGenerationResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public string? QuerySqlContent { get; init; }
    public string? UpdateSqlContent { get; init; }
    public string? OutputDirectory { get; init; }
    public bool FilesCreated { get; init; }
}
