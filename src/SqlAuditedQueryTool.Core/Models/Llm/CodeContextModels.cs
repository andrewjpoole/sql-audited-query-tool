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

/// <summary>
/// Result of general code analysis.
/// </summary>
public sealed class CodeAnalysisResult
{
    public required string Directory { get; init; }
    public required List<ClassAnalysis> Classes { get; init; }
    public required int TotalClasses { get; init; }
    public required int DbRelatedClasses { get; init; }
    public List<EntityFrameworkContext>? EfContexts { get; init; }
}

/// <summary>
/// Analysis of a C# class.
/// </summary>
public sealed class ClassAnalysis
{
    public required string Name { get; init; }
    public string? Namespace { get; init; }
    public required string FilePath { get; init; }
    public List<string> BaseTypes { get; init; } = new();
    public List<PropertySummary> Properties { get; init; } = new();
    public List<MethodSummary> Methods { get; init; } = new();
    public List<string> Attributes { get; init; } = new();
    public bool IsDbRelated { get; init; }
    public string? DbTechnology { get; init; } // "EF Core", "Dapper", "ADO.NET"
    public DapperUsage? DapperDetails { get; init; }
    public AdoNetUsage? AdoNetDetails { get; init; }
}

/// <summary>
/// Summary of a property.
/// </summary>
public sealed class PropertySummary
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public List<string> Attributes { get; init; } = new();
}

/// <summary>
/// Summary of a method.
/// </summary>
public sealed class MethodSummary
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public List<string> Parameters { get; init; } = new();
    public List<string> Attributes { get; init; } = new();
}

/// <summary>
/// Details of Dapper usage in a class.
/// </summary>
public sealed class DapperUsage
{
    public List<string> QueryMethods { get; init; } = new();
    public List<string> SqlSnippets { get; init; } = new();
    public bool HasDapperUsing { get; init; }
}

/// <summary>
/// Details of ADO.NET usage in a class.
/// </summary>
public sealed class AdoNetUsage
{
    public List<string> ConnectionTypes { get; init; } = new();
    public List<string> CommandTypes { get; init; } = new();
    public List<string> ExecuteMethods { get; init; } = new();
    public bool HasAdoNetUsing { get; init; }
}
