# Decision: Qwen 3.5 Thinking Mode — Explicit Opt-In via Config

**Date:** 2025-07-18
**Author:** Radagast (LLM Engineer)
**Status:** Implemented

## Context
Switching from qwen2.5 to qwen3.5 broke the pipeline because qwen3.5 models think by default, injecting thinking tokens into responses. The Ollama API now separates thinking content via `think` request parameter and `thinking` response field.

## Decision
- **ThinkingEnabled defaults to `false`** — small models (0.8B–4B) are too slow when thinking; opt-in for larger models
- **Dual-layer thinking content removal:** structured `Message.Thinking` field is ignored (preferred), inline `<think>` tag stripping kept as fallback
- **Both streaming and non-streaming paths** respect the config via OllamaSharp's native support
- **No model name workarounds needed** — OllamaSharp 5.4.23 supports `ChatRequest.Think` and `ChatOptions.AddOllamaOption(OllamaOption.Think, ...)` natively

## Impact
- Backward compatible with qwen2.5 (non-thinking models)
- Andrew can switch to qwen3.5:9b by changing model name in config and optionally enabling `ThinkingEnabled: true`
- All 113 existing tests pass
