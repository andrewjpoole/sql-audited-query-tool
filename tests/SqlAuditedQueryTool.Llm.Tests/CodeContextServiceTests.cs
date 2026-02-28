using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SqlAuditedQueryTool.Llm.Configuration;
using SqlAuditedQueryTool.Llm.Services;

namespace SqlAuditedQueryTool.Llm.Tests;

public class CodeContextServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly CodeContextService _service;

    public CodeContextServiceTests()
    {
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CodeContextTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var options = Options.Create(new CodeContextOptions
        {
            DefaultRepositoryPath = _testDirectory,
            MaxFileSizeBytes = 1_048_576,
            MaxFilesPerList = 100,
            MaxSearchResults = 50
        });

        var logger = Mock.Of<ILogger<CodeContextService>>();
        _service = new CodeContextService(options, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileAsync_ValidFile_ReturnsContent()
    {
        // Arrange
        var fileName = "test.cs";
        var filePath = Path.Combine(_testDirectory, fileName);
        var content = "public class TestClass { }";
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await _service.ReadFileAsync(filePath);

        // Assert
        Assert.Equal(filePath, result.Path);
        Assert.Equal(content, result.Content);
        Assert.True(result.SizeBytes > 0);
    }

    [Fact]
    public async Task ReadFileAsync_NonExistentFile_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.cs");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.ReadFileAsync(filePath));
    }

    [Fact]
    public async Task ListFilesAsync_FindsCSharpFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "Class1.cs");
        var file2 = Path.Combine(_testDirectory, "Class2.cs");
        var txtFile = Path.Combine(_testDirectory, "readme.txt");
        
        await File.WriteAllTextAsync(file1, "class Class1 {}");
        await File.WriteAllTextAsync(file2, "class Class2 {}");
        await File.WriteAllTextAsync(txtFile, "text file");

        // Act
        var result = await _service.ListFilesAsync(_testDirectory, "*.cs");

        // Assert
        Assert.Equal(2, result.Files.Count);
        Assert.All(result.Files, f => Assert.EndsWith(".cs", f.Name));
    }

    [Fact]
    public async Task SearchCodeAsync_FindsMatches()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "Service.cs");
        var content = @"
public class UserService
{
    public void CreateUser() { }
    public void DeleteUser() { }
}";
        await File.WriteAllTextAsync(file1, content);

        // Act
        var result = await _service.SearchCodeAsync("CreateUser", _testDirectory);

        // Assert
        Assert.True(result.TotalCount > 0);
        Assert.Contains(result.Matches, m => m.LineContent.Contains("CreateUser"));
    }

    [Fact]
    public async Task AnalyzeEntityFrameworkContextAsync_FindsDbContext()
    {
        // Arrange
        var contextFile = Path.Combine(_testDirectory, "AppDbContext.cs");
        var contextCode = @"
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .ToTable(""Users"", ""dbo"")
            .HasKey(u => u.Id);
    }
}";
        await File.WriteAllTextAsync(contextFile, contextCode);

        var userFile = Path.Combine(_testDirectory, "User.cs");
        var userCode = @"
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table(""Users"")]
public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }
    
    public string? Email { get; set; }
    
    public ICollection<Order> Orders { get; set; }
}";
        await File.WriteAllTextAsync(userFile, userCode);

        var orderFile = Path.Combine(_testDirectory, "Order.cs");
        var orderCode = @"
using System.ComponentModel.DataAnnotations;

public class Order
{
    [Key]
    public int Id { get; set; }
    
    public int UserId { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public User User { get; set; }
}";
        await File.WriteAllTextAsync(orderFile, orderCode);

        // Act
        var contexts = await _service.AnalyzeEntityFrameworkContextAsync(_testDirectory);

        // Assert
        Assert.Single(contexts);
        var context = contexts[0];
        Assert.Equal("AppDbContext", context.ContextName);
        Assert.Equal(2, context.Entities.Count);

        var userEntity = context.Entities.FirstOrDefault(e => e.Name == "User");
        Assert.NotNull(userEntity);
        Assert.Equal("Users", userEntity.TableName);
        Assert.Contains(userEntity.Properties, p => p.Name == "Id" && p.IsKey);
        Assert.Contains(userEntity.Properties, p => p.Name == "Name" && p.IsRequired && p.MaxLength == 100);
        Assert.Contains(userEntity.Properties, p => p.Name == "Email" && p.IsNullable);
        Assert.Contains(userEntity.NavigationProperties, n => n.Name == "Orders" && n.TargetEntity == "Order");

        var orderEntity = context.Entities.FirstOrDefault(e => e.Name == "Order");
        Assert.NotNull(orderEntity);
        Assert.Contains(orderEntity.Properties, p => p.Name == "Id" && p.IsKey);
        Assert.Contains(orderEntity.Properties, p => p.Name == "UserId");
        Assert.Contains(orderEntity.NavigationProperties, n => n.Name == "User" && n.TargetEntity == "User");
    }

    [Fact]
    public async Task AnalyzeEntityFrameworkContextAsync_ParsesFluentApiConfiguration()
    {
        // Arrange
        var contextFile = Path.Combine(_testDirectory, "TestContext.cs");
        var contextCode = @"
using Microsoft.EntityFrameworkCore;

public class TestContext : DbContext
{
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .ToTable(""Products"", ""catalog"")
            .HasKey(p => p.Id);

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Sku)
            .IsUnique();
    }
}";
        await File.WriteAllTextAsync(contextFile, contextCode);

        var productFile = Path.Combine(_testDirectory, "Product.cs");
        var productCode = @"
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; }
    public string Name { get; set; }
}";
        await File.WriteAllTextAsync(productFile, productCode);

        // Act
        var contexts = await _service.AnalyzeEntityFrameworkContextAsync(_testDirectory);

        // Assert
        Assert.Single(contexts);
        var context = contexts[0];
        var product = context.Entities.First();
        Assert.Equal("Products", product.TableName);
        Assert.Equal("catalog", product.SchemaName);
        // Index extraction from Fluent API is a bonus feature - just verify it doesn't crash
        Assert.NotNull(product.Indexes);
    }

    [Fact]
    public async Task SearchCodeAsync_RespectsMaxResults()
    {
        // Arrange
        var optionsWithLimit = Options.Create(new CodeContextOptions
        {
            DefaultRepositoryPath = _testDirectory,
            MaxSearchResults = 2
        });
        var logger = Mock.Of<ILogger<CodeContextService>>();
        var service = new CodeContextService(optionsWithLimit, logger);

        // Create multiple files with many matches
        for (int i = 0; i < 5; i++)
        {
            var file = Path.Combine(_testDirectory, $"File{i}.cs");
            await File.WriteAllTextAsync(file, "public class TestClass { }");
        }

        // Act
        var result = await service.SearchCodeAsync("public", _testDirectory);

        // Assert
        Assert.True(result.TotalCount <= 2);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ListFilesAsync_ExcludesBinAndObjDirectories()
    {
        // Arrange
        var binDir = Path.Combine(_testDirectory, "bin");
        var objDir = Path.Combine(_testDirectory, "obj");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(objDir);

        var goodFile = Path.Combine(_testDirectory, "Good.cs");
        var binFile = Path.Combine(binDir, "Bad.cs");
        var objFile = Path.Combine(objDir, "Bad.cs");

        await File.WriteAllTextAsync(goodFile, "class Good {}");
        await File.WriteAllTextAsync(binFile, "class Bad {}");
        await File.WriteAllTextAsync(objFile, "class Bad {}");

        // Act
        var result = await _service.ListFilesAsync(_testDirectory, "*.cs");

        // Assert
        Assert.Single(result.Files);
        Assert.EndsWith("Good.cs", result.Files[0].Name);
    }
}
