namespace SqlAuditedQueryTool.Llm.Configuration;

/// <summary>
/// Configuration options for code context reading and analysis.
/// </summary>
public sealed class CodeContextOptions
{
    public const string SectionName = "CodeContext";

    /// <summary>
    /// Default repository path for code analysis. If not set, uses current directory.
    /// </summary>
    public string? DefaultRepositoryPath { get; set; }

    /// <summary>
    /// Directories allowed for file access (whitelist). Empty list means all directories allowed.
    /// </summary>
    public List<string> AllowedDirectories { get; set; } = new();

    /// <summary>
    /// File patterns to exclude from searches and listings.
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new()
    {
        "**/bin/**",
        "**/obj/**",
        "**/node_modules/**",
        "**/.git/**",
        "**/.vs/**"
    };

    /// <summary>
    /// Maximum file size in bytes that can be read. Default is 1MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 1_048_576; // 1MB

    /// <summary>
    /// Maximum number of files to return in a single ListFiles call.
    /// </summary>
    public int MaxFilesPerList { get; set; } = 100;

    /// <summary>
    /// Maximum number of search results to return.
    /// </summary>
    public int MaxSearchResults { get; set; } = 50;
}
