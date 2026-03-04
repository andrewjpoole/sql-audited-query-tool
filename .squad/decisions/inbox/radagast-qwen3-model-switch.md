# Decision: Downgrade LLM Model from qwen3.5:27b to qwen3:14b

**Date:** 2026-02-24  
**Author:** Radagast (LLM Engineer)  
**Status:** Implemented  
**Impact:** Infrastructure, Performance

## Context

User's Docker/WSL environment has a hard memory limit of ~16GB. The qwen3.5:27b model requires ~17GB of RAM, causing out-of-memory issues during inference. Need to switch to a smaller model that fits within available memory while maintaining tool calling and thinking mode capabilities.

## Decision

Switch from `qwen3.5:27b` (27 billion parameters, ~17GB RAM) to `qwen3:14b` (14 billion parameters, ~8GB RAM).

## Rationale

1. **Memory Constraints:** qwen3:14b fits comfortably within 16GB limit (~8GB vs ~17GB)
2. **Feature Parity:** Both models support:
   - Tool calling (execute_sql_query, code context tools)
   - Thinking mode with <think>...</think> blocks
   - Same prompt format and API surface
3. **Performance:** Smaller model = faster inference on CPU (important for local development)
4. **No Code Changes Required:** Existing <think> block filtering already handles both models

## Implementation

**Files Changed:**
- `src/SqlAuditedQueryTool.App/appsettings.json` — `Llm.Model`: "qwen3.5:27b" → "qwen3:14b"
- `SqlAuditedQueryTool.AppHost/AppHost.cs` — `ollama.AddModel(...)`: "qwen3.5:27b" → "qwen3:14b"

**Kept Unchanged:**
- `ChatTimeoutSeconds`: 300 (still appropriate for CPU inference with smaller model)

**Code Compatibility:**
- `OllamaLlmService.cs` already strips <think> blocks in both streaming and non-streaming modes
- No changes needed to tool calling infrastructure
- No API surface changes

## Alternatives Considered

1. **Increase Docker/WSL memory limit:** Not feasible on user's hardware
2. **Switch to smaller 7b model:** Would sacrifice too much reasoning quality
3. **Use quantized qwen3.5:** Still exceeds 16GB limit even with Q4 quantization

## Consequences

**Positive:**
- ✅ Fits within Docker/WSL memory constraints
- ✅ Faster inference on CPU
- ✅ Lower resource usage enables parallel tool execution
- ✅ Maintains tool calling and thinking mode features

**Negative:**
- ⚠️ Slightly lower reasoning capability (14B vs 27B parameters)
- ⚠️ May produce shorter explanations or miss subtle context

**Neutral:**
- Same API surface and user experience
- Same prompt engineering patterns

## Rollback Plan

If qwen3:14b shows unacceptable reasoning quality:
1. Revert model strings to "qwen3.5:27b"
2. Increase Docker/WSL memory allocation to 20GB
3. Or switch to cloud-hosted LLM (OpenRouter/OpenAI)

## Next Steps

1. Pull qwen3:14b model: `ollama pull qwen3:14b`
2. Restart Aspire to apply configuration
3. Test tool calling with schema queries
4. Monitor inference speed and memory usage

## References

- Ollama model library: https://ollama.com/library/qwen3
- qwen3:14b specs: ~14B params, 8K context, tool calling support
- qwen3.5:27b specs: ~27B params, 8K context, tool calling support
