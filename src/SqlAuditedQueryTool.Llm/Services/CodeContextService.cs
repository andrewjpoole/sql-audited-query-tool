using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Llm.Configuration;

namespace SqlAuditedQueryTool.Llm.Services;

/// <summary>
/// Service for reading and analyzing code context using file system and Roslyn.
/// </summary>
public sealed class CodeContextService : ICodeContextService
{
    private readonly CodeContextOptions _options;
    private readonly ILogger<CodeContextService> _logger;
    private readonly HashSet<string> _sessionAllowedDirectories = new();

    public CodeContextService(
        IOptions<CodeContextOptions> options,
        ILogger<CodeContextService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileContent> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        ValidateFilePath(normalizedPath);

        _logger.LogDebug("Reading file: {Path}", normalizedPath);

        var fileInfo = new FileInfo(normalizedPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File not found: {normalizedPath}");
        }

        if (fileInfo.Length > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File size ({fileInfo.Length} bytes) exceeds maximum allowed ({_options.MaxFileSizeBytes} bytes)");
        }

        var content = await File.ReadAllTextAsync(normalizedPath, cancellationToken);

        return new FileContent
        {
            Path = normalizedPath,
            Content = content,
            SizeBytes = fileInfo.Length,
            LastModified = fileInfo.LastWriteTimeUtc
        };
    }

    public async Task<FileListResult> ListFilesAsync(
        string directory,
        string pattern = "*.cs",
        CancellationToken cancellationToken = default)
    {
        var normalizedDir = NormalizePath(directory);
        ValidateDirectoryPath(normalizedDir);

        _logger.LogDebug("Listing files in {Directory} with pattern {Pattern}", normalizedDir, pattern);

        var dirInfo = new DirectoryInfo(normalizedDir);
        if (!dirInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalizedDir}");
        }

        var allFiles = dirInfo.GetFiles(pattern, SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f.FullName))
            .OrderBy(f => f.FullName)
            .ToList();

        var truncated = allFiles.Count > _options.MaxFilesPerList;
        var files = allFiles
            .Take(_options.MaxFilesPerList)
            .Select(f => new FileListResult.FileInfo
            {
                Path = f.FullName,
                Name = f.Name,
                SizeBytes = f.Length,
                LastModified = f.LastWriteTimeUtc
            })
            .ToList();

        return new FileListResult
        {
            Directory = normalizedDir,
            Files = files,
            TotalCount = allFiles.Count,
            Truncated = truncated
        };
    }

    public async Task<CodeSearchResult> SearchCodeAsync(
        string searchPattern,
        string directory = ".",
        CancellationToken cancellationToken = default)
    {
        var normalizedDir = NormalizePath(directory);
        ValidateDirectoryPath(normalizedDir);

        _logger.LogDebug("Searching code in {Directory} for pattern: {Pattern}", normalizedDir, searchPattern);

        var dirInfo = new DirectoryInfo(normalizedDir);
        if (!dirInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalizedDir}");
        }

        var regex = new Regex(searchPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var matches = new List<CodeSearchResult.SearchMatch>();

        var csFiles = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f.FullName));

        foreach (var file in csFiles)
        {
            if (matches.Count >= _options.MaxSearchResults)
                break;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lines = await File.ReadAllLinesAsync(file.FullName, cancellationToken);
                for (int i = 0; i < lines.Length && matches.Count < _options.MaxSearchResults; i++)
                {
                    if (regex.IsMatch(lines[i]))
                    {
                        var context = GetLineContext(lines, i, contextLines: 2);
                        matches.Add(new CodeSearchResult.SearchMatch
                        {
                            FilePath = file.FullName,
                            LineNumber = i + 1,
                            LineContent = lines[i],
                            Context = context
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching file: {Path}", file.FullName);
            }
        }

        return new CodeSearchResult
        {
            SearchPattern = searchPattern,
            Matches = matches,
            TotalCount = matches.Count,
            Truncated = matches.Count >= _options.MaxSearchResults
        };
    }

    public async Task<CodeAnalysisResult> AnalyzeCodeAsync(
        string directory = ".",
        CancellationToken cancellationToken = default)
    {
        var normalizedDir = NormalizePath(directory);
        ValidateDirectoryPath(normalizedDir);

        _logger.LogInformation("Analyzing application code in: {Directory}", normalizedDir);

        var classes = new List<ClassAnalysis>();
        var dirInfo = new DirectoryInfo(normalizedDir);

        if (!dirInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalizedDir}");
        }

        // Find all C# files
        var csFiles = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f.FullName))
            .ToList();

        foreach (var file in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var code = await File.ReadAllTextAsync(file.FullName, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
                var root = await tree.GetRootAsync(cancellationToken);

                // Find all classes
                var classDeclarations = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>();

                foreach (var classDecl in classDeclarations)
                {
                    var classAnalysis = AnalyzeClass(classDecl, file.FullName, code);
                    classes.Add(classAnalysis);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing file: {Path}", file.FullName);
            }
        }

        // Analyze EF Core contexts in detail
        var efContexts = await AnalyzeEntityFrameworkContextAsync(directory, cancellationToken);
        var dbRelatedCount = classes.Count(c => c.IsDbRelated);

        _logger.LogInformation(
            "Analyzed {Total} classes ({DbRelated} database-related), {EF} EF contexts",
            classes.Count, dbRelatedCount, efContexts.Count);

        return new CodeAnalysisResult
        {
            Directory = normalizedDir,
            Classes = classes,
            TotalClasses = classes.Count,
            DbRelatedClasses = dbRelatedCount,
            EfContexts = efContexts.Count > 0 ? efContexts : null
        };
    }

    public async Task<List<EntityFrameworkContext>> AnalyzeEntityFrameworkContextAsync(
        string directory = ".",
        CancellationToken cancellationToken = default)
    {
        var normalizedDir = NormalizePath(directory);
        ValidateDirectoryPath(normalizedDir);

        _logger.LogInformation("Analyzing Entity Framework contexts in: {Directory}", normalizedDir);

        var contexts = new List<EntityFrameworkContext>();
        var dirInfo = new DirectoryInfo(normalizedDir);

        if (!dirInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalizedDir}");
        }

        // Find all C# files
        var csFiles = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f.FullName))
            .ToList();

        foreach (var file in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var code = await File.ReadAllTextAsync(file.FullName, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
                var root = await tree.GetRootAsync(cancellationToken);

                // Find DbContext classes
                var dbContextClasses = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => InheritsFromDbContext(c));

                foreach (var contextClass in dbContextClasses)
                {
                    var context = await AnalyzeDbContextClassAsync(contextClass, file.FullName, code, cancellationToken);
                    if (context != null)
                    {
                        contexts.Add(context);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing file: {Path}", file.FullName);
            }
        }

        _logger.LogInformation("Found {Count} DbContext classes", contexts.Count);
        return contexts;
    }

    private ClassAnalysis AnalyzeClass(ClassDeclarationSyntax classDecl, string filePath, string sourceCode)
    {
        var className = classDecl.Identifier.Text;
        var namespaceName = GetNamespace(classDecl);
        var baseTypes = GetBaseTypes(classDecl);
        var properties = GetPropertySummaries(classDecl);
        var methods = GetMethodSummaries(classDecl);
        var attributes = GetAttributes(classDecl.AttributeLists).Select(a => a.ToString()).ToList();

        // Determine if class is database-related and what technology
        var isDbRelated = false;
        string? dbTechnology = null;
        DapperUsage? dapperDetails = null;
        AdoNetUsage? adoNetDetails = null;

        if (baseTypes.Any(bt => bt.Contains("DbContext")))
        {
            isDbRelated = true;
            dbTechnology = "EF Core";
        }
        else
        {
            dapperDetails = DetectDapperUsage(sourceCode);
            if (dapperDetails != null)
            {
                isDbRelated = true;
                dbTechnology = "Dapper";
            }
            else
            {
                adoNetDetails = DetectAdoNetUsage(sourceCode);
                if (adoNetDetails != null)
                {
                    isDbRelated = true;
                    dbTechnology = "ADO.NET";
                }
            }
        }

        return new ClassAnalysis
        {
            Name = className,
            Namespace = namespaceName,
            FilePath = filePath,
            BaseTypes = baseTypes,
            Properties = properties,
            Methods = methods,
            Attributes = attributes,
            IsDbRelated = isDbRelated,
            DbTechnology = dbTechnology,
            DapperDetails = dapperDetails,
            AdoNetDetails = adoNetDetails
        };
    }

    private DapperUsage? DetectDapperUsage(string sourceCode)
    {
        var hasDapperUsing = sourceCode.Contains("using Dapper;");
        var queryMethods = new List<string>();
        var sqlSnippets = new List<string>();

        // Detect Dapper query methods
        var dapperPatterns = new[] {
            ".Query<",
            ".QueryAsync<",
            ".Execute(",
            ".ExecuteAsync(",
            ".QueryFirst",
            ".QuerySingle",
            ".QueryMultiple",
            "SqlMapper."
        };

        foreach (var pattern in dapperPatterns)
        {
            if (sourceCode.Contains(pattern))
            {
                queryMethods.Add(pattern.TrimStart('.').TrimEnd('('));
            }
        }

        // Extract SQL snippets (simplified - looks for string literals that look like SQL)
        var sqlRegex = new Regex(@"""(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP)\s+[^""]{10,}""", RegexOptions.IgnoreCase);
        var sqlMatches = sqlRegex.Matches(sourceCode);
        foreach (Match match in sqlMatches)
        {
            var sql = match.Value.Substring(1, Math.Min(100, match.Value.Length - 2)); // First 100 chars
            if (sql.Length < match.Value.Length - 2) sql += "...";
            sqlSnippets.Add(sql);
        }

        if (hasDapperUsing || queryMethods.Any())
        {
            return new DapperUsage
            {
                HasDapperUsing = hasDapperUsing,
                QueryMethods = queryMethods,
                SqlSnippets = sqlSnippets.Take(5).ToList() // Limit to 5 snippets
            };
        }

        return null;
    }

    private AdoNetUsage? DetectAdoNetUsage(string sourceCode)
    {
        var hasAdoNetUsing = sourceCode.Contains("using System.Data.SqlClient;") ||
                             sourceCode.Contains("using Microsoft.Data.SqlClient;") ||
                             sourceCode.Contains("using System.Data.Common;");

        var connectionTypes = new List<string>();
        var commandTypes = new List<string>();
        var executeMethods = new List<string>();

        // Detect ADO.NET types
        var adoNetTypes = new[] {
            "SqlConnection",
            "SqlCommand",
            "SqlDataReader",
            "DbConnection",
            "DbCommand",
            "DbDataReader"
        };

        foreach (var type in adoNetTypes)
        {
            if (sourceCode.Contains(type))
            {
                if (type.Contains("Connection"))
                    connectionTypes.Add(type);
                else if (type.Contains("Command"))
                    commandTypes.Add(type);
            }
        }

        // Detect execute methods
        var executePatterns = new[] {
            ".ExecuteReader(",
            ".ExecuteNonQuery(",
            ".ExecuteScalar(",
            "new SqlCommand(",
            "new SqlConnection("
        };

        foreach (var pattern in executePatterns)
        {
            if (sourceCode.Contains(pattern))
            {
                executeMethods.Add(pattern.TrimStart('.').TrimEnd('(').Replace("new ", ""));
            }
        }

        if (hasAdoNetUsing || connectionTypes.Any() || commandTypes.Any() || executeMethods.Any())
        {
            return new AdoNetUsage
            {
                HasAdoNetUsing = hasAdoNetUsing,
                ConnectionTypes = connectionTypes.Distinct().ToList(),
                CommandTypes = commandTypes.Distinct().ToList(),
                ExecuteMethods = executeMethods.Distinct().ToList()
            };
        }

        return null;
    }

    private string GetNamespace(ClassDeclarationSyntax classDecl)
    {
        var namespaceDecl = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
            return namespaceDecl.Name.ToString();

        var fileScopedNs = classDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        return fileScopedNs?.Name.ToString() ?? "Global";
    }

    private List<string> GetBaseTypes(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
            return new List<string>();

        return classDecl.BaseList.Types.Select(t => t.ToString()).ToList();
    }

    private List<PropertySummary> GetPropertySummaries(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .Select(p => new PropertySummary
            {
                Name = p.Identifier.Text,
                Type = p.Type.ToString(),
                Attributes = GetAttributes(p.AttributeLists).Select(a => a.ToString()).ToList()
            })
            .ToList();
    }

    private List<MethodSummary> GetMethodSummaries(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Select(m => new MethodSummary
            {
                Name = m.Identifier.Text,
                ReturnType = m.ReturnType.ToString(),
                Parameters = m.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}").ToList(),
                Attributes = GetAttributes(m.AttributeLists).Select(a => a.ToString()).ToList()
            })
            .ToList();
    }

    private async Task<EntityFrameworkContext?> AnalyzeDbContextClassAsync(
        ClassDeclarationSyntax contextClass,
        string filePath,
        string sourceCode,
        CancellationToken cancellationToken)
    {
        var contextName = contextClass.Identifier.Text;
        _logger.LogDebug("Analyzing DbContext: {Name}", contextName);

        var entities = new List<EntityDefinition>();

        // Find DbSet properties (these are the entities)
        var dbSetProperties = contextClass.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => IsDbSetProperty(p));

        foreach (var dbSet in dbSetProperties)
        {
            var entityType = ExtractDbSetEntityType(dbSet);
            if (entityType != null)
            {
                // Try to find the entity class definition
                var entity = await FindAndAnalyzeEntityAsync(entityType, Path.GetDirectoryName(filePath)!, cancellationToken);
                if (entity != null)
                {
                    entities.Add(entity);
                }
            }
        }

        // Parse OnModelCreating for Fluent API configurations
        var onModelCreating = contextClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "OnModelCreating");

        if (onModelCreating != null)
        {
            ParseFluentApiConfigurations(onModelCreating, entities);
        }

        return new EntityFrameworkContext
        {
            ContextName = contextName,
            FilePath = filePath,
            Entities = entities
        };
    }

    private async Task<EntityDefinition?> FindAndAnalyzeEntityAsync(
        string entityTypeName,
        string searchDirectory,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Looking for entity class: {EntityType}", entityTypeName);

        // Search for the entity class file
        var dirInfo = new DirectoryInfo(searchDirectory);
        var csFiles = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f.FullName));

        foreach (var file in csFiles)
        {
            try
            {
                var code = await File.ReadAllTextAsync(file.FullName, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
                var root = await tree.GetRootAsync(cancellationToken);

                var classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == entityTypeName);

                if (classDeclaration != null)
                {
                    return AnalyzeEntityClass(classDeclaration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching entity in file: {Path}", file.FullName);
            }
        }

        _logger.LogWarning("Could not find entity class: {EntityType}", entityTypeName);
        return null;
    }

    private EntityDefinition AnalyzeEntityClass(ClassDeclarationSyntax classDeclaration)
    {
        var properties = new List<PropertyDefinition>();
        var navigationProperties = new List<NavigationProperty>();

        var classAttributes = GetAttributes(classDeclaration.AttributeLists);
        var tableName = ExtractTableName(classAttributes);
        var schemaName = ExtractSchemaName(classAttributes);

        foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propName = property.Identifier.Text;
            var propType = property.Type.ToString();
            var attributes = GetAttributes(property.AttributeLists);

            // Check if it's a navigation property (collection or reference to another entity)
            if (IsNavigationProperty(propType))
            {
                var navProp = AnalyzeNavigationProperty(property, attributes);
                if (navProp != null)
                {
                    navigationProperties.Add(navProp);
                }
            }
            else
            {
                // Regular property
                properties.Add(new PropertyDefinition
                {
                    Name = propName,
                    Type = propType,
                    IsNullable = IsNullableType(propType),
                    IsKey = attributes.Contains("Key") || propName.Equals("Id", StringComparison.OrdinalIgnoreCase),
                    IsRequired = attributes.Contains("Required"),
                    MaxLength = ExtractMaxLength(attributes),
                    ColumnName = ExtractColumnName(attributes),
                    DataAnnotations = attributes
                });
            }
        }

        return new EntityDefinition
        {
            Name = classDeclaration.Identifier.Text,
            TableName = tableName,
            SchemaName = schemaName,
            Properties = properties,
            NavigationProperties = navigationProperties,
            Indexes = ExtractIndexes(classAttributes),
            Configurations = new Dictionary<string, string>()
        };
    }

    private NavigationProperty? AnalyzeNavigationProperty(
        PropertyDeclarationSyntax property,
        List<string> attributes)
    {
        var propType = property.Type.ToString();
        var propName = property.Identifier.Text;

        string targetEntity;
        string relationType;

        // Determine if it's a collection navigation
        if (IsCollectionType(propType))
        {
            targetEntity = ExtractCollectionElementType(propType) ?? "Unknown";
            relationType = "OneToMany";
        }
        else
        {
            targetEntity = propType;
            relationType = "ManyToOne";
        }

        var foreignKey = attributes
            .FirstOrDefault(a => a.StartsWith("ForeignKey("))
            ?.Replace("ForeignKey(", "").TrimEnd(')').Trim('"');

        var inverseProperty = attributes
            .FirstOrDefault(a => a.StartsWith("InverseProperty("))
            ?.Replace("InverseProperty(", "").TrimEnd(')').Trim('"');

        return new NavigationProperty
        {
            Name = propName,
            TargetEntity = targetEntity,
            RelationType = relationType,
            ForeignKey = foreignKey,
            InverseProperty = inverseProperty
        };
    }

    private void ParseFluentApiConfigurations(
        MethodDeclarationSyntax onModelCreating,
        List<EntityDefinition> entities)
    {
        // Parse method body for Entity<T>() calls and configurations
        // This is a simplified parser - could be enhanced for more complex scenarios
        var bodyText = onModelCreating.Body?.ToString() ?? "";

        foreach (var entity in entities)
        {
            // Look for configurations like: modelBuilder.Entity<EntityName>()
            var entityPattern = new Regex(
                $@"Entity<{entity.Name}>\(\).*?(?=Entity<|$)",
                RegexOptions.Singleline);

            var match = entityPattern.Match(bodyText);
            if (match.Success)
            {
                var configText = match.Value;

                // Extract ToTable configuration
                var tableMatch = Regex.Match(configText, @"ToTable\([""']([^""']+)[""'](?:,\s*[""']([^""']+)[""'])?\)");
                if (tableMatch.Success)
                {
                    entity.TableName = tableMatch.Groups[1].Value;
                    if (tableMatch.Groups.Count > 2 && !string.IsNullOrEmpty(tableMatch.Groups[2].Value))
                    {
                        entity.SchemaName = tableMatch.Groups[2].Value;
                    }
                }

                // Extract HasKey configuration
                var keyMatch = Regex.Match(configText, @"HasKey\(.*?\)");
                if (keyMatch.Success)
                {
                    entity.Configurations["HasKey"] = keyMatch.Value;
                }

                // Extract indexes
                var indexMatches = Regex.Matches(configText, @"HasIndex\([^)]+\)");
                foreach (Match indexMatch in indexMatches)
                {
                    entity.Indexes.Add(indexMatch.Value);
                }
            }
        }
    }

    #region Helper Methods

    private bool InheritsFromDbContext(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.BaseList?.Types
            .Any(t => t.ToString().Contains("DbContext")) ?? false;
    }

    private bool IsDbSetProperty(PropertyDeclarationSyntax property)
    {
        return property.Type.ToString().StartsWith("DbSet<");
    }

    private string? ExtractDbSetEntityType(PropertyDeclarationSyntax property)
    {
        var typeText = property.Type.ToString();
        var match = Regex.Match(typeText, @"DbSet<(\w+)>");
        return match.Success ? match.Groups[1].Value : null;
    }

    private bool IsNavigationProperty(string type)
    {
        return IsCollectionType(type) || (!IsPrimitiveType(type) && !type.Contains("string"));
    }

    private bool IsCollectionType(string type)
    {
        return type.StartsWith("ICollection<") ||
               type.StartsWith("List<") ||
               type.StartsWith("IEnumerable<") ||
               type.StartsWith("IList<");
    }

    private bool IsPrimitiveType(string type)
    {
        var primitives = new[] { "int", "long", "short", "byte", "bool", "decimal", "double", "float", "string", "DateTime", "DateTimeOffset", "Guid", "TimeSpan" };
        var baseType = type.TrimEnd('?');
        return primitives.Any(p => baseType == p || baseType == $"System.{p}");
    }

    private bool IsNullableType(string type)
    {
        return type.EndsWith("?") || type.StartsWith("Nullable<");
    }

    private string? ExtractCollectionElementType(string type)
    {
        var match = Regex.Match(type, @"<(\w+)>");
        return match.Success ? match.Groups[1].Value : null;
    }

    private List<string> GetAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var attributes = new List<string>();
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                attributes.Add(attribute.ToString());
            }
        }
        return attributes;
    }

    private string? ExtractTableName(List<string> attributes)
    {
        var tableAttr = attributes.FirstOrDefault(a => a.StartsWith("Table("));
        if (tableAttr == null) return null;

        var match = Regex.Match(tableAttr, @"Table\([""']([^""']+)[""']");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractSchemaName(List<string> attributes)
    {
        var tableAttr = attributes.FirstOrDefault(a => a.StartsWith("Table("));
        if (tableAttr == null) return null;

        var match = Regex.Match(tableAttr, @"Schema\s*=\s*[""']([^""']+)[""']");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? ExtractColumnName(List<string> attributes)
    {
        var columnAttr = attributes.FirstOrDefault(a => a.StartsWith("Column("));
        if (columnAttr == null) return null;

        var match = Regex.Match(columnAttr, @"Column\([""']([^""']+)[""']");
        return match.Success ? match.Groups[1].Value : null;
    }

    private int? ExtractMaxLength(List<string> attributes)
    {
        var maxLenAttr = attributes.FirstOrDefault(a => a.StartsWith("MaxLength("));
        if (maxLenAttr == null) return null;

        var match = Regex.Match(maxLenAttr, @"MaxLength\((\d+)\)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var len) ? len : null;
    }

    private List<string> ExtractIndexes(List<string> attributes)
    {
        return attributes.Where(a => a.StartsWith("Index(")).ToList();
    }

    private string GetLineContext(string[] lines, int lineIndex, int contextLines)
    {
        var sb = new StringBuilder();
        var start = Math.Max(0, lineIndex - contextLines);
        var end = Math.Min(lines.Length - 1, lineIndex + contextLines);

        for (int i = start; i <= end; i++)
        {
            var marker = i == lineIndex ? ">>> " : "    ";
            sb.AppendLine($"{marker}{i + 1}: {lines[i]}");
        }

        return sb.ToString();
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return _options.DefaultRepositoryPath ?? Directory.GetCurrentDirectory();
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }

    private void ValidateFilePath(string path)
    {
        var allAllowedDirectories = GetAllowedDirectories();
        if (allAllowedDirectories.Count > 0)
        {
            var allowed = allAllowedDirectories.Any(dir =>
                path.StartsWith(NormalizePath(dir), StringComparison.OrdinalIgnoreCase));

            if (!allowed)
            {
                throw new UnauthorizedAccessException($"Access to file denied: {path}");
            }
        }

        if (IsExcluded(path))
        {
            throw new UnauthorizedAccessException($"File is in excluded path: {path}");
        }
    }

    private void ValidateDirectoryPath(string path)
    {
        var allAllowedDirectories = GetAllowedDirectories();
        if (allAllowedDirectories.Count > 0)
        {
            var normalizedPath = NormalizePath(path);
            var allowed = allAllowedDirectories.Any(dir =>
                normalizedPath.StartsWith(NormalizePath(dir), StringComparison.OrdinalIgnoreCase));

            if (!allowed)
            {
                throw new UnauthorizedAccessException($"Access to directory denied: {path}");
            }
        }
    }

    public void AddAllowedDirectory(string directory)
    {
        var normalized = NormalizePath(directory);
        if (!Directory.Exists(normalized))
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalized}");
        }
        _sessionAllowedDirectories.Add(normalized);
        _logger.LogInformation("Added allowed directory for session: {Directory}", normalized);
    }

    public bool RemoveAllowedDirectory(string directory)
    {
        var normalized = NormalizePath(directory);
        var removed = _sessionAllowedDirectories.Remove(normalized);
        if (removed)
        {
            _logger.LogInformation("Removed allowed directory from session: {Directory}", normalized);
        }
        return removed;
    }

    public List<string> GetAllowedDirectories()
    {
        var all = new List<string>(_options.AllowedDirectories);
        all.AddRange(_sessionAllowedDirectories);
        return all;
    }

    private bool IsExcluded(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        return _options.ExcludePatterns.Any(pattern =>
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*\\*/", ".*")
                .Replace("\\*", "[^/]*") + "$";
            return Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase);
        });
    }

    #endregion
}
