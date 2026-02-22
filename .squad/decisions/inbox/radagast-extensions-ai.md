### 2026-02-22: Migrated LLM layer to Microsoft.Extensions.AI — Radagast
**By:** Radagast (LLM Engineer)
**What:** Replaced direct OllamaSharp usage in `SqlAuditedQueryTool.Llm` with `Microsoft.Extensions.AI` abstractions (`IChatClient`).
**Changes:**
- `SqlAuditedQueryTool.Llm.csproj`: `OllamaSharp` → `Microsoft.Extensions.AI`
- `OllamaLlmService`: depends on `IChatClient` instead of `IOllamaApiClient`; uses `GetResponseAsync()` / `GetStreamingResponseAsync()`
- `Program.cs`: bridges `IOllamaApiClient` (from Aspire) to `IChatClient` via explicit cast in DI
- The Llm project no longer has any OllamaSharp dependency — it's provider-agnostic
**Why:** `IChatClient` is the standard .NET abstraction for LLM clients. This decouples the Llm layer from any specific provider, making it easy to swap Ollama for another backend (Azure OpenAI, local ONNX, etc.) without touching the Llm project. The App composition root remains the only place that knows about OllamaSharp.
**Constraints:** OllamaSharp must remain in App project (via CommunityToolkit.Aspire.OllamaSharp) for Aspire service discovery. Data safety boundary unchanged.
