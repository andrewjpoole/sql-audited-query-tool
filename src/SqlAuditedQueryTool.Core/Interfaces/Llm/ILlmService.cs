namespace SqlAuditedQueryTool.Core.Interfaces.Llm;

/// <summary>
/// Local LLM service for chat interactions. The LLM can now execute queries via tool calling.
/// </summary>
public interface ILlmService
{
    Task<Models.Llm.LlmResponse> ChatAsync(Models.Llm.LlmChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatAsync(Models.Llm.LlmChatRequest request, CancellationToken cancellationToken = default);

    Task<string> ExecuteToolCallAsync(Models.Llm.ToolCallRequest toolCall, CancellationToken cancellationToken = default);
}
