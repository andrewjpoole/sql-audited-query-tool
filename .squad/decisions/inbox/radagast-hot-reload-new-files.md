# Hot Reload Does Not Detect New Files

**Date:** 2026-02-24  
**Decided by:** Radagast (LLM Engineer)  
**Status:** Documented  

## Context

After implementing the progressive boost fix for autocomplete, the fix wasn't active in the running application despite:
- Restarting via Aspire
- Waiting for embeddings to load
- Hard resetting the browser

## Root Cause

Aspire's hot reload mechanism (and .NET hot reload in general) watches for **changes to existing files**, NOT for **new files being added**. The new service files (`EmbeddingCompletionService.cs`, `SchemaEmbeddingService.cs`, etc.) were untracked in git and never picked up by the running application.

## Decision

**When adding new files to the project:**

1. **Always do a full rebuild cycle:**
   - Stop all running processes
   - Run `dotnet build` explicitly
   - Restart the application

2. **Don't rely on Aspire restart alone:**
   - "Restart" in Aspire only restarts the process
   - It does NOT trigger a rebuild
   - New files won't be included in the running binary

3. **Track new files in git immediately:**
   - Files showing `??` in git status are at risk
   - Hot reload can't track what git doesn't know about
   - Run `git add <files>` after creating new services

## Consequences

**Good:**
- Prevents "fix not working" debugging sessions when new code isn't loaded
- Forces proper build verification before testing
- Ensures DLLs contain the latest code

**Bad:**
- Extra manual step required (stop → build → start)
- Slower development cycle when adding new files
- Less convenient than hot reload for existing files

## Verification

To verify a fix is actually running:
1. Check git status - no `??` files for critical changes
2. Run `dotnet build` and ensure it succeeds
3. Check DLL timestamps match or are newer than process start time
4. Look for "file is locked" errors indicating stale processes

## Related

- Hot reload works great for changes to existing files
- This only affects NEW files being added to the project
- Aspire dashboard shows restart but doesn't show rebuild status
