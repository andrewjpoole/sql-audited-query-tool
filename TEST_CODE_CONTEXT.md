# Testing the Code Context System

## Quick Test

1. **Start the application:**
   ```bash
   dotnet run --project SqlAuditedQueryTool.AppHost
   ```

2. **In the chat interface, try these questions:**

   ```
   "What entities are defined in my codebase?"
   ```
   The LLM will call `AnalyzeEntityFrameworkContext` and list all DbContext classes and entities.

   ```
   "Show me the properties of the Account entity"
   ```
   The LLM will analyze the entity and show properties with types, nullability, and data annotations.

   ```
   "What relationships does the Deposit entity have?"
   ```
   The LLM will show navigation properties and foreign keys.

   ```
   "Search for code containing 'ApplicationIntent'"
   ```
   The LLM will call `SearchCode` and find where readonly connection strings are configured.

## Manual Testing with Test Project

If you want to test the analyzers directly:

```bash
cd tests/SqlAuditedQueryTool.Llm.Tests
dotnet test --filter CodeContext --logger "console;verbosity=detailed"
```

This runs all Code Context tests with detailed output showing:
- Entity discovery
- Property extraction
- Fluent API parsing
- File reading
- Code search

## Configuration for Testing

Add to `appsettings.Development.json`:

```json
{
  "CodeContext": {
    "DefaultRepositoryPath": "D:\\git\\sql-audited-query-tool\\src",
    "AllowedDirectories": [
      "D:\\git\\sql-audited-query-tool"
    ],
    "MaxFileSizeBytes": 1048576
  }
}
```

## Expected Output

When asking "What entities are in the codebase?", the LLM should respond with something like:

```
I found the following entities in your codebase:

From SqlAuditedQueryToolDbContext:
1. Partner (table: Partners)
   - Properties: PartnerID, PartnerCode, PartnerName, Status, OnboardedDate, ApiKey...
   
2. User (table: Users)
   - Properties: UserID, Username, Email, FullName, Role, Department...
   
3. DepositLocation (table: DepositLocations)
   - Properties: LocationID, LocationCode, LocationType, LocationName...

... (and more entities)

Each entity has navigation properties showing relationships to other entities.
Would you like me to show you the details of a specific entity?
```

## Debug Logging

To see what tools the LLM is calling, add to `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "SqlAuditedQueryTool.Llm": "Debug"
    }
  }
}
```

You'll see log entries like:
```
[CodeContextService] AnalyzeEntityFrameworkContext called for: D:\git\sql-audited-query-tool\src
[CodeContextService] Found 8 entities in 1 DbContext
```

## Current Status

✅ All 10 tests passing
✅ Service registered in DI
✅ Tools available to LLM
✅ Configuration loaded
✅ Ready to use
