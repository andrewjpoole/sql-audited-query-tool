# Ollama MCP Integration - Technical Feasibility Assessment

**Author:** Radagast (LLM Engineer)  
**Date:** February 22, 2026  
**Context:** Re-evaluating SQL Server MCP integration now that local Ollama models CAN access database data

---

## Executive Summary

**YES - Ollama can be connected to SQL Server MCP**, but it requires an **intermediary bridge/proxy**. Ollama does NOT have native MCP support, but has robust tool calling capabilities. Multiple proven bridge solutions exist that translate between Ollama's tool calling API and MCP's protocol.

**Recommended Approach:** Use an existing MCP bridge (TypeScript or Python) as a sidecar service.

---

## 1. Ollama MCP Support Analysis

### ✅ Ollama Has: Tool Calling (Since v0.6+)
- **Native tool calling API** introduced July 2024
- **OpenAPI-compatible** tool definitions
- Supports **function calling** with structured outputs
- Works with models like:
  - Llama 3.1/3.2/3.3
  - Qwen 2.5 (what we use: `qwen2.5-coder:7b`)
  - Mistral Nemo
  - Firefunction v2

**Evidence:**
```javascript
// Ollama API supports tools parameter
const response = await ollama.chat({
  model: 'llama3.1',
  messages: [{role: 'user', content: 'What is the weather?'}],
  tools: [{
    type: 'function',
    function: {
      name: 'get_weather',
      description: 'Get current weather',
      parameters: {
        type: 'object',
        properties: { city: { type: 'string' } }
      }
    }
  }]
});
```

### ❌ Ollama Does NOT Have: Native MCP Support
- No built-in MCP client
- No MCP protocol handlers
- Ollama only speaks HTTP JSON (REST API)
- MCP uses JSON-RPC over stdio/SSE/HTTP

**Gap:** Ollama cannot directly connect to MCP servers.

---

## 2. Microsoft.Extensions.AI.Ollama Tool Support

### Current State (9.7.0-preview.1)
The `Microsoft.Extensions.AI.Ollama` package:
- ✅ Supports `IChatClient` abstraction
- ✅ Has chat completion API
- ❓ **Tool calling support unclear** from NuGet documentation

### Current Implementation Analysis
Looking at our code (`OllamaLlmService.cs`):
```csharp
// We only use basic chat - no tools
var response = await _client.GetResponseAsync(messages, cancellationToken: cancellationToken);
```

**Finding:** We're not currently using any tool calling features from Microsoft.Extensions.AI.

### Research Needed
Need to check if `IChatClient` supports passing tools/functions to Ollama. The abstraction might already support it.

---

## 3. Actual Integration Options

### Option A: Ollama MCP Bridge (TypeScript) ⭐ RECOMMENDED
**Repository:** [patruff/ollama-mcp-bridge](https://github.com/patruff/ollama-mcp-bridge) (962 stars)

**How it works:**
1. Node.js service sits between your .NET app and Ollama
2. Connects to MCP servers via stdio/SSE/HTTP
3. Proxies Ollama API at `http://localhost:8000` (configurable)
4. Automatically translates tool calls → MCP calls

**Architecture:**
```
.NET App → Ollama MCP Bridge (Node.js) → Ollama + MCP Servers
                ↓
            SQL Server MCP
```

**Configuration Example:**
```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "node",
      "args": ["path/to/sql-server-mcp/index.js"],
      "env": {
        "CONNECTION_STRING": "..."
      }
    }
  },
  "llm": {
    "model": "qwen2.5-coder:7b",
    "baseUrl": "http://localhost:11434"
  }
}
```

**Pros:**
- ✅ Battle-tested (962 stars, active development)
- ✅ Supports multiple MCP servers simultaneously
- ✅ Transparent proxy (100% Ollama API compatible)
- ✅ Handles tool call orchestration automatically
- ✅ Streaming support
- ✅ Multi-round tool execution

**Cons:**
- ❌ Requires Node.js runtime
- ❌ Another service to manage
- ❌ Adds latency (minimal, but present)

---

### Option B: Ollama MCP Bridge (Python)
**Repository:** [jonigl/ollama-mcp-bridge](https://github.com/jonigl/ollama-mcp-bridge) (60 stars)

**How it works:**
- FastAPI-based proxy server
- Same concept as TypeScript version
- Available as PyPI package: `ollama-mcp-bridge`

**Pros:**
- ✅ Python-based (might be easier to integrate with .NET)
- ✅ PyPI package (`pip install ollama-mcp-bridge`)
- ✅ Docker support
- ✅ Similar feature set to TypeScript version

**Cons:**
- ❌ Less mature (60 vs 962 stars)
- ❌ Python runtime required

---

### Option C: Direct Integration - Custom .NET MCP Client
**Build our own bridge in C#**

**How it would work:**
```csharp
// 1. Call Ollama with tools
var response = await ollama.ChatAsync(messages, tools);

// 2. If tool_calls present, execute via MCP SDK
if (response.ToolCalls != null) {
    foreach (var call in response.ToolCalls) {
        var result = await mcpClient.CallToolAsync(call.Name, call.Arguments);
        // Feed back to Ollama
    }
}
```

**Required:**
- C# MCP SDK: [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)
- Custom orchestration logic
- Tool call loop management

**Pros:**
- ✅ No external dependencies (Node.js/Python)
- ✅ Full control over implementation
- ✅ Native .NET performance

**Cons:**
- ❌ Most development work
- ❌ Need to implement orchestration ourselves
- ❌ C# MCP SDK less mature than Node/Python
- ❌ Would need to verify Microsoft.Extensions.AI.Ollama supports tools

---

### Option D: MCP as REST API (Custom Wrapper)
**Build a REST API wrapper around SQL Server MCP**

**How it would work:**
```
.NET App → Custom REST API → SQL Server MCP (stdio)
    ↓
  Ollama (tool calling)
```

**Implementation:**
```csharp
// 1. Define tools for Ollama
var tools = new[] {
    new Tool {
        Name = "execute_query",
        Description = "Execute SQL query",
        Parameters = { /* schema */ }
    }
};

// 2. Call Ollama
var response = await ollama.ChatAsync(messages, tools);

// 3. Execute via REST wrapper
if (response.ToolCalls.Any(t => t.Name == "execute_query")) {
    var result = await http.PostAsync("http://localhost:5000/execute", ...);
}
```

**Pros:**
- ✅ Simple HTTP-based integration
- ✅ No complex protocol handling
- ✅ Easy to test/debug

**Cons:**
- ❌ Need to build REST wrapper ourselves
- ❌ Loses MCP ecosystem benefits
- ❌ Not reusable with other MCP clients

---

## 4. Recommended Architecture

### **Primary Recommendation: Option A (TypeScript Bridge)**

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET Application                          │
│                                                              │
│  ┌──────────────────┐                                       │
│  │ OllamaLlmService │ ───────────────────────┐             │
│  └──────────────────┘                        │             │
└──────────────────────────────────────────────┼─────────────┘
                                               │
                                               ▼
                         http://localhost:8000 (Bridge API)
                        ┌────────────────────────────┐
                        │  ollama-mcp-bridge (Node)  │
                        │                            │
                        │  ┌──────────────────────┐  │
                        │  │  Tool Orchestrator   │  │
                        │  └──────────────────────┘  │
                        │           │                │
                        │           ▼                │
                        │  ┌──────────────────────┐  │
                        │  │   MCP Clients        │  │
                        │  └──────────────────────┘  │
                        └────────────────────────────┘
                                   │        │
                    ┌──────────────┘        └──────────────┐
                    ▼                                       ▼
          ┌─────────────────┐                    ┌──────────────────┐
          │ SQL Server MCP  │                    │     Ollama       │
          │   (stdio)       │                    │  localhost:11434 │
          └─────────────────┘                    └──────────────────┘
                    │                                      │
                    ▼                                      ▼
            SQL Server Database               qwen2.5-coder:7b
```

### Changes Required

#### 1. Configuration (`appsettings.json`)
```json
{
  "Llm": {
    "BaseUrl": "http://localhost:8000",  // Bridge instead of Ollama
    "Model": "qwen2.5-coder:7b"
  }
}
```

#### 2. Bridge Configuration (`mcp-config.json`)
```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-sqlserver",
        "Server=localhost;Database=MyDb;..."
      ]
    }
  },
  "llm": {
    "model": "qwen2.5-coder:7b",
    "baseUrl": "http://localhost:11434"
  }
}
```

#### 3. Deployment
```bash
# Terminal 1: Start Ollama
ollama serve

# Terminal 2: Start MCP Bridge
npm install -g ollama-mcp-bridge
ollama-mcp-bridge --config mcp-config.json --port 8000

# Terminal 3: Run .NET app
dotnet run --project src/SqlAuditedQueryTool.App
```

---

## 5. Implementation Complexity Assessment

### Option A (Bridge): Low-Medium Complexity
**Effort:** 2-4 hours
- Install Node.js (if not present)
- Configure bridge with SQL Server MCP
- Update .NET config to point to bridge
- Test integration

**Risk:** Low - proven solution

---

### Option C (Custom .NET): High Complexity
**Effort:** 2-3 days
- Learn C# MCP SDK
- Implement tool orchestration
- Handle multi-round tool calls
- Error handling & retries
- Testing

**Risk:** Medium - new code, less battle-tested

---

## 6. Security Implications (Updated Context)

### Original Constraint (No Longer Applies)
~~"LLM must never see database data"~~

### New Reality
✅ **Ollama running locally CAN access database data**
- Ollama = local infrastructure
- Data never leaves Andrew's environment
- Air gap was about EXTERNAL services (OpenAI, Anthropic)

### What This Means for SQL Server MCP
✅ **SQL Server MCP can now be used fully**
- Execute queries ✅
- Return row-level results ✅
- Access schema metadata ✅
- No data sanitization needed ✅

### Remaining Security Considerations
1. **Read-only enforcement** - still critical
2. **Audit logging** - still required (GitHub issues)
3. **Query validation** - prevent malicious SQL

---

## 7. Proof of Concept Plan

### Phase 1: Local Testing (1-2 hours)
```bash
# 1. Install bridge
npm install -g ollama-mcp-bridge

# 2. Configure SQL Server MCP
cat > mcp-config.json <<EOF
{
  "mcpServers": {
    "sqlserver": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sqlserver", "..."]
    }
  }
}
EOF

# 3. Start bridge
ollama-mcp-bridge --config mcp-config.json

# 4. Test with curl
curl http://localhost:8000/api/chat -d '{
  "model": "qwen2.5-coder:7b",
  "messages": [
    {"role": "user", "content": "What tables are in the database?"}
  ]
}'
```

### Phase 2: .NET Integration (1-2 hours)
1. Update `OllamaOptions.BaseUrl` → bridge URL
2. Test existing chat functionality
3. Verify tool calls work transparently
4. Check streaming responses

### Phase 3: Production Readiness (2-4 hours)
1. Add bridge to Aspire orchestration
2. Configure health checks
3. Add error handling
4. Update documentation

---

## 8. Final Recommendation

### ✅ YES - Integrate SQL Server MCP

**Use:** Ollama MCP Bridge (TypeScript version)

**Why:**
1. **Proven solution** - 962 stars, active development
2. **Zero code changes** to our .NET app
3. **Battle-tested** tool orchestration
4. **Transparent** - just change BaseUrl
5. **Extensible** - can add more MCP servers later

### Next Steps
1. **Gandalf:** Decide if we want MCP integration
2. **Radagast:** Implement PoC with bridge (if approved)
3. **Testing:** Verify tool calls work with SQL Server
4. **Documentation:** Update README with MCP setup

---

## 9. Alternative: Keep Current Architecture

If we DON'T want the bridge dependency:

### Keep What We Have
- Schema metadata only (current approach)
- Simple, no external dependencies
- Works great for query suggestions
- LLM never sees actual data (but can now!)

### When to Consider MCP
- Want agent-like behavior (query → analyze → refine)
- Need access to multiple data sources
- Want to leverage MCP ecosystem

---

## Conclusion

**Ollama + MCP = Possible ✅**  
**Practical = Yes, via bridge**  
**Recommended = TypeScript bridge (Option A)**  
**Complexity = Low-Medium**  
**Timeline = 4-8 hours total**

The updated security context (local Ollama CAN access data) makes SQL Server MCP integration more valuable since we can now provide full query results, not just schema metadata.

**Decision point for Gandalf:** Do we want agent-like SQL capabilities, or is metadata-only sufficient for the use case?
