using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Llm.Services;
using Moq;

namespace SqlAuditedQueryTool.Llm.Tests.Services;

public class EmbeddingCompletionServiceTests
{
    [Fact]
    public async Task GetSchemaCompletionsAsync_KeywordPrefixMatch_ReturnsKeywordFirst()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockVectorStore = new Mock<IVectorStore>();
        
        var dummyEmbedding = new float[768];
        mockEmbedding.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyEmbedding);

        // Create mock results - schema item has higher base score but keyword matches prefix
        var mockKeywordResults = new List<VectorSearchResult>
        {
            new VectorSearchResult
            {
                Key = "keyword:SELECT",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "keyword",
                    DisplayText = "SELECT",
                    Description = "T-SQL keyword: SELECT",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Keyword" },
                        { "type", "keyword" }
                    }
                },
                Score = 0.7f // Lower base score
            }
        };

        var mockSchemaResults = new List<VectorSearchResult>
        {
            new VectorSearchResult
            {
                Key = "column:dbo.Users.SelectedDate",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = "SelectedDate",
                    Description = "SelectedDate (datetime) NULL",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Field" },
                        { "type", "datetime" },
                        { "schema", "dbo" },
                        { "table", "Users" },
                        { "column", "SelectedDate" }
                    }
                },
                Score = 0.95f // Higher base score but doesn't match prefix "SELE"
            }
        };

        // Setup mock to return different results based on category
        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "schema", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSchemaResults);

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "keyword", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockKeywordResults);

        var service = new EmbeddingCompletionService(mockEmbedding.Object, mockVectorStore.Object);
        var context = new CompletionContext("SELE", "SELE", 4);

        // Act
        var result = await service.GetSchemaCompletionsAsync(context);

        // Assert
        Assert.NotEmpty(result);
        // SELECT should be first due to prefix match boost
        Assert.Equal("SELECT", result.First().Label);
    }

    [Fact]
    public async Task GetSchemaCompletionsAsync_AfterFrom_ReturnsOnlyTables()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockVectorStore = new Mock<IVectorStore>();
        
        var dummyEmbedding = new float[768];
        mockEmbedding.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyEmbedding);

        var mockSchemaResults = new List<VectorSearchResult>
        {
            new VectorSearchResult
            {
                Key = "table:dbo.Users",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = "dbo.Users",
                    Description = "Table: dbo.Users",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Class" },
                        { "type", "table" },
                        { "schema", "dbo" },
                        { "table", "Users" }
                    }
                },
                Score = 0.9f
            },
            new VectorSearchResult
            {
                Key = "column:dbo.Users.UserId",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = "UserId",
                    Description = "UserId (int) NOT NULL",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Field" },
                        { "type", "int" },
                        { "schema", "dbo" },
                        { "table", "Users" },
                        { "column", "UserId" }
                    }
                },
                Score = 0.85f
            }
        };

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "schema", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSchemaResults);

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "keyword", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var service = new EmbeddingCompletionService(mockEmbedding.Object, mockVectorStore.Object);
        var context = new CompletionContext("SELECT * FROM ", "SELECT * FROM ", 15);

        // Act
        var result = await service.GetSchemaCompletionsAsync(context);

        // Assert
        Assert.NotEmpty(result);
        // After FROM should only show tables
        Assert.All(result, item => Assert.Equal("table", item.Detail));
    }

    [Fact]
    public async Task GetSchemaCompletionsAsync_AfterTableDot_ReturnsOnlyColumns()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockVectorStore = new Mock<IVectorStore>();
        
        var dummyEmbedding = new float[768];
        mockEmbedding.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyEmbedding);

        var mockSchemaResults = new List<VectorSearchResult>
        {
            new VectorSearchResult
            {
                Key = "table:dbo.Users",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = "dbo.Users",
                    Description = "Table: dbo.Users",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Class" },
                        { "type", "table" },
                        { "schema", "dbo" },
                        { "table", "Users" }
                    }
                },
                Score = 0.9f
            },
            new VectorSearchResult
            {
                Key = "column:dbo.Users.UserId",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = "UserId",
                    Description = "UserId (int) NOT NULL",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Field" },
                        { "type", "int" },
                        { "schema", "dbo" },
                        { "table", "Users" },
                        { "column", "UserId" }
                    }
                },
                Score = 0.85f
            }
        };

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "schema", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSchemaResults);

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "keyword", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var service = new EmbeddingCompletionService(mockEmbedding.Object, mockVectorStore.Object);
        var context = new CompletionContext("SELECT Users.", "SELECT Users.", 13);

        // Act
        var result = await service.GetSchemaCompletionsAsync(context);

        // Assert
        Assert.NotEmpty(result);
        // After table.dot should only show columns
        Assert.All(result, item => 
        {
            Assert.NotNull(item.Detail);
            Assert.NotEqual("table", item.Detail);
        });
    }

    [Fact]
    public async Task GetSchemaCompletionsAsync_TablePrefixMatch_BoostsMatchingTable()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockVectorStore = new Mock<IVectorStore>();
        
        var dummyEmbedding = new float[768];
        mockEmbedding.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyEmbedding);

        var mockSchemaResults = new List<VectorSearchResult>
        {
            new VectorSearchResult
            {
                Key = "table:dbo.Users",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = "dbo.Users",
                    Description = "Table: dbo.Users",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Class" },
                        { "type", "table" },
                        { "schema", "dbo" },
                        { "table", "Users" }
                    }
                },
                Score = 0.5f // Lower semantic score
            },
            new VectorSearchResult
            {
                Key = "table:dbo.Accounts",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = "dbo.Accounts",
                    Description = "Table: dbo.Accounts",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Class" },
                        { "type", "table" },
                        { "schema", "dbo" },
                        { "table", "Accounts" }
                    }
                },
                Score = 0.9f // Higher semantic score but doesn't match prefix
            }
        };

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "schema", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSchemaResults);

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "keyword", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var service = new EmbeddingCompletionService(mockEmbedding.Object, mockVectorStore.Object);
        var context = new CompletionContext("SELECT * FROM u", "SELECT * FROM u", 15);

        // Act
        var result = await service.GetSchemaCompletionsAsync(context);

        // Assert
        Assert.NotEmpty(result);
        // dbo.Users should be first due to prefix match "u"
        var firstTable = result.First();
        Assert.Contains("Users", firstTable.Label);
    }

    [Fact]
    public async Task GetSchemaCompletionsAsync_EmptyPrefix_ReturnsResults()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockVectorStore = new Mock<IVectorStore>();
        
        var dummyEmbedding = new float[768];
        mockEmbedding.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyEmbedding);

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        var service = new EmbeddingCompletionService(mockEmbedding.Object, mockVectorStore.Object);
        var context = new CompletionContext("", "", 0);

        // Act
        var result = await service.GetSchemaCompletionsAsync(context);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("SEL", "SELECT")]
    [InlineData("SELE", "SELECT")]
    [InlineData("SELEC", "SELECT")]
    [InlineData("FRO", "FROM")]
    [InlineData("WHER", "WHERE")]
    [InlineData("ORD", "ORDER BY")]
    public async Task GetSchemaCompletionsAsync_PartialKeyword_ReturnsMatchingKeyword(string input, string expectedKeyword)
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockVectorStore = new Mock<IVectorStore>();
        
        var dummyEmbedding = new float[768];
        mockEmbedding.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyEmbedding);

        var mockKeywordResults = new List<VectorSearchResult>
        {
            new VectorSearchResult
            {
                Key = $"keyword:{expectedKeyword}",
                Embedding = dummyEmbedding,
                Metadata = new VectorMetadata
                {
                    Category = "keyword",
                    DisplayText = expectedKeyword,
                    Description = $"T-SQL keyword: {expectedKeyword}",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Keyword" },
                        { "type", "keyword" }
                    }
                },
                Score = 0.6f
            }
        };

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "schema", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        mockVectorStore.Setup(x => x.SearchAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            "keyword", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockKeywordResults);

        var service = new EmbeddingCompletionService(mockEmbedding.Object, mockVectorStore.Object);
        var context = new CompletionContext(input, input, input.Length);

        // Act
        var result = await service.GetSchemaCompletionsAsync(context);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(expectedKeyword, result.First().Label);
    }
}
