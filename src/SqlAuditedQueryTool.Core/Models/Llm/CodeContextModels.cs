namespace SqlAuditedQueryTool.Core.Models.Llm;

/// <summary>
/// Result of Entity Framework context analysis.
/// </summary>
public sealed class EntityFrameworkContext
{
    public required string ContextName { get; init; }
    public required string FilePath { get; init; }
    public required List<EntityDefinition> Entities { get; init; }
}

/// <summary>
/// Represents an Entity Framework entity class.
/// </summary>
public sealed class EntityDefinition
{
    public required string Name { get; init; }
    public string? TableName { get; set; }
    public string? SchemaName { get; set; }
    public required List<PropertyDefinition> Properties { get; init; }
    public required List<NavigationProperty> NavigationProperties { get; init; }
    public required List<string> Indexes { get; init; }
    public required Dictionary<string, string> Configurations { get; init; }
}

/// <summary>
/// Represents a property on an entity.
/// </summary>
public sealed class PropertyDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsNullable { get; init; }
    public bool IsKey { get; init; }
    public bool IsRequired { get; init; }
    public int? MaxLength { get; init; }
    public string? ColumnName { get; init; }
    public List<string> DataAnnotations { get; init; } = new();
}

/// <summary>
/// Represents a navigation property (relationship).
/// </summary>
public sealed class NavigationProperty
{
    public required string Name { get; init; }
    public required string TargetEntity { get; init; }
    public required string RelationType { get; init; } // "OneToMany", "ManyToOne", "OneToOne", "ManyToMany"
    public string? ForeignKey { get; init; }
    public string? InverseProperty { get; init; }
}

/// <summary>
/// Result of file reading operation.
/// </summary>
public sealed class FileContent
{
    public required string Path { get; init; }
    public required string Content { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModified { get; init; }
}

/// <summary>
/// Result of file listing operation.
/// </summary>
public sealed class FileListResult
{
    public required string Directory { get; init; }
    public required List<FileInfo> Files { get; init; }
    public required int TotalCount { get; init; }
    public bool Truncated { get; init; }

    public sealed class FileInfo
    {
        public required string Path { get; init; }
        public required string Name { get; init; }
        public required long SizeBytes { get; init; }
        public required DateTime LastModified { get; init; }
    }
}

/// <summary>
/// Result of code search operation.
/// </summary>
public sealed class CodeSearchResult
{
    public required string SearchPattern { get; init; }
    public required List<SearchMatch> Matches { get; init; }
    public required int TotalCount { get; init; }
    public bool Truncated { get; init; }

    public sealed class SearchMatch
    {
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string LineContent { get; init; }
        public required string Context { get; init; } // Surrounding lines
    }
}
