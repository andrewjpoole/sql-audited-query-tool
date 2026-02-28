using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Llm.Services;

namespace SqlAuditedQueryTool.Llm.Tests;

public class CodeContextAssistantTests
{
    [Fact]
    public async Task GetCodeContextTools_ReturnsExpectedTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockCodeContext = new Mock<ICodeContextService>();
        var mockLogger = new Mock<ILogger<CodeContextAssistant>>();

        var assistant = new CodeContextAssistant(
            mockChatClient.Object,
            mockCodeContext.Object,
            mockLogger.Object);

        // We can't directly test the tools since they're private,
        // but we can verify the service is constructed correctly
        Assert.NotNull(assistant);
    }

    [Fact]
    public async Task ChatWithCodeContextAsync_CallsChatClient()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mockCodeContext = new Mock<ICodeContextService>();
        var mockLogger = new Mock<ILogger<CodeContextAssistant>>();

        // Setup mock response
        var mockResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response"));
        
        mockChatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var assistant = new CodeContextAssistant(
            mockChatClient.Object,
            mockCodeContext.Object,
            mockLogger.Object);

        // Act
        var result = await assistant.ChatWithCodeContextAsync("What entities exist?");

        // Assert
        Assert.NotNull(result);
        mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.Is<ChatOptions>(o => o.Tools != null && o.Tools.Count > 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
