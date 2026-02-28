# Testing Tool Calls Fix

## Setup
1. Start the application:
   ```bash
   dotnet run --project src/SqlAuditedQueryTool.App
   ```

2. Navigate to the chat interface at http://localhost:5173

## Test Cases

### Test 1: List Context Directories
**User message:** "Show me what directories I can access"

**Expected behavior:**
- LLM should call `ListContextDirectories` tool
- Tool should execute and return the list of allowed directories
- Response should include the directory list in natural language

**What to look for in logs:**
- `Extracted tool call: ListContextDirectories`
- `Total tool calls extracted: 1`
- Tool execution success

### Test 2: Add Context Directory
**User message:** "Add D:\git\sql-audited-query-tool\src to my allowed directories"

**Expected behavior:**
- LLM should call `AddContextDirectory` tool with the directory path
- Tool should execute and add the directory
- Response should confirm the directory was added

### Test 3: Analyze Entity Framework Context
**User message:** "What Entity Framework entities are defined in my codebase?"

**Expected behavior:**
- LLM should call `AnalyzeEntityFrameworkContext` tool
- Tool should scan for DbContext classes
- Response should list the entities found

### Test 4: Read File
**User message:** "Show me the content of src/SqlAuditedQueryTool.Core/Interfaces/IQueryExecutor.cs"

**Expected behavior:**
- LLM should call `ReadFile` tool with the file path
- Tool should return the file contents
- Response should summarize or present the file content

## Verification Checklist

✅ Build succeeds without errors
✅ All 96 tests pass
✅ Application starts without errors
✅ Tool calls are extracted from LLM response
✅ Tools execute successfully
✅ LLM receives and processes tool results
✅ Natural language response includes tool output

## Debug Logs to Watch

Enable debug logging in `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "SqlAuditedQueryTool.Llm": "Debug"
    }
  }
}
```

Look for:
- `Sending chat request to Ollama model {Model}`
- `Received response from Ollama`
- `Extracted tool call: {ToolName}`
- `Total tool calls extracted: {Count}`
- `Executing tool call: {ToolName}`
