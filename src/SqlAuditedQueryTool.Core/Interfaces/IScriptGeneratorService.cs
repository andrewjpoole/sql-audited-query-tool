using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Core.Interfaces;

/// <summary>
/// Service for generating sql-script-runner scripts.
/// </summary>
public interface IScriptGeneratorService
{
    ScriptGenerationResult GenerateScripts(ScriptGenerationRequest request);
}
