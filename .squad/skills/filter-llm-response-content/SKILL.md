# Skill: Filter LLM Response Content

**When to Use:** When integrating LLM models that include internal reasoning, thinking mode, or other metadata tags in their responses that should not be shown to end users.

**Problem:** Modern LLM models (like qwen3.5) may include thinking/reasoning content in responses using XML-like tags (e.g., `<think>...</think>`, `<reasoning>...</reasoning>`). This content:
- Clutters the user interface
- May cause parsing errors in downstream code
- Exposes internal model behavior users don't need to see
- Increases token counts and response size

## Implementation Pattern

### 1. Create a Filtering Method

``csharp
private static string StripInternalContent(string text)
{
    if (string.IsNullOrEmpty(text))
        return text;

    var pattern = new System.Text.RegularExpressions.Regex(
        @"<(think|reasoning|internal)>.*?</\1>\s*",
        System.Text.RegularExpressions.RegexOptions.Singleline | 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    
    return pattern.Replace(text, string.Empty).Trim();
}
``

### 2. Apply to Non-Streaming and Streaming Responses

Apply `StripInternalContent()` to both `ChatAsync()` and `StreamChatAsync()` before returning content to users.

## Key Tags

- `<think>...</think>` — qwen3.5 thinking mode
- `<reasoning>...</reasoning>` — some experimental models
- `<internal>...</internal>` — potential future use

## Reference

- `src/SqlAuditedQueryTool.Llm/Services/OllamaLlmService.cs`
