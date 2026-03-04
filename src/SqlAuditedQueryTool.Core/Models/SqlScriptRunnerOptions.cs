namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Configuration options for sql-script-runner integration.
/// </summary>
public sealed class SqlScriptRunnerOptions
{
    public const string SectionName = "SqlScriptRunner";
    
    public string ReposBaseDirectory { get; set; } = "";
    public Dictionary<string, string> Repositories { get; set; } = new();
}
