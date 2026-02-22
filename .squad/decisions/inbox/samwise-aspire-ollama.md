### 2026-02-22: Aspire Ollama Integration — Samwise
**By:** Samwise (Backend)
**What:** Wired Ollama into Aspire AppHost using CommunityToolkit packages. OllamaLlmService now uses OllamaSharp's `IOllamaApiClient` via Aspire service discovery instead of raw HttpClient to hardcoded localhost:11434.
**Key details:**
- AppHost resource name for the model is `"ollamaModel"` (clean name, separate from model identifier `qwen2.5-coder:7b`).
- Client registration uses `builder.AddOllamaApiClient("ollamaModel")` in App's Program.cs.
- LLM DI registration changed from `AddHttpClient<ILlmService, OllamaLlmService>` to `AddScoped<ILlmService, OllamaLlmService>` since `IOllamaApiClient` comes from Aspire.
- Default model changed from `llama3.2` to `qwen2.5-coder:7b`.
**Why:** Aspire service discovery replaces hardcoded URLs, enabling container orchestration and consistent connection management across environments.
**Constraints:** OllamaLlmService still reads model name from `OllamaOptions` configuration for flexibility.
