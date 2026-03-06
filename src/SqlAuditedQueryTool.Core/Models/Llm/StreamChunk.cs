namespace SqlAuditedQueryTool.Core.Models.Llm;

/// <summary>
/// Represents a single chunk of streamed LLM output, distinguishing
/// regular text content from thinking/reasoning content.
/// </summary>
public sealed record StreamChunk(string Content, bool IsThinking = false);
