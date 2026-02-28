using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Core.Interfaces.Llm;

/// <summary>
/// Service for reading and analyzing code context to assist the LLM.
/// </summary>
public interface ICodeContextService
{
    /// <summary>
    /// Read the content of a specific file.
    /// </summary>
    Task<FileContent> ReadFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// List files in a directory matching a pattern.
    /// </summary>
    Task<FileListResult> ListFilesAsync(string directory, string pattern = "*.cs", CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for code patterns across files.
    /// </summary>
    Task<CodeSearchResult> SearchCodeAsync(string searchPattern, string directory = ".", CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze Entity Framework DbContext and extract entity definitions.
    /// </summary>
    Task<List<EntityFrameworkContext>> AnalyzeEntityFrameworkContextAsync(string directory = ".", CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a directory to the allowed list for this session.
    /// </summary>
    void AddAllowedDirectory(string directory);

    /// <summary>
    /// Remove a directory from the session allowed list.
    /// </summary>
    bool RemoveAllowedDirectory(string directory);

    /// <summary>
    /// Get all allowed directories (both config and session).
    /// </summary>
    List<string> GetAllowedDirectories();
}
