# Write Query Simulator/Predictor - Implementation Plan

## Problem Statement

The SQL Audited Query Tool is strictly read-only, but users often discover issues that need fixing via UPDATE, INSERT, or DELETE statements. We need a safe way to:
1. Help users craft write scripts to fix discovered issues
2. Simulate/predict the effects of these scripts WITHOUT executing them
3. Generate commit-ready scripts for a separate sql-script-runner tool

## Goals

- ✅ **Safety First**: Never execute write queries against the database
- ✅ **Predictive Analysis**: Simulate what WOULD happen if a script was run
- ✅ **Clear Separation**: Distinct UI from read-only query pane
- ✅ **LLM-Assisted**: Use AI to help craft and validate write scripts
- ✅ **Integration Ready**: Output format compatible with sql-script-runner

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    User Interface                           │
├─────────────────────┬───────────────────────────────────────┤
│  Read Query Pane    │  Write Script Simulator Pane (NEW)    │
│  (existing)         │  - Script editor                      │
│                     │  - Simulate/Predict button            │
│                     │  - Prediction results display         │
│                     │  - Affected rows preview              │
│                     │  - Commit to repo button              │
└─────────────────────┴───────────────────────────────────────┘
           │                           │
           ▼                           ▼
    ┌─────────────┐          ┌──────────────────┐
    │ Query LLM   │          │ Simulator LLM    │
    │ (existing)  │          │ (NEW)            │
    │             │          │ Different prompt │
    └─────────────┘          └──────────────────┘
                                      │
                    ┌─────────────────┴────────────────┐
                    ▼                                  ▼
            ┌───────────────┐              ┌──────────────────┐
            │  Simulation   │              │ Script Generator │
            │  Engine       │              │                  │
            │  (NEW)        │              │  - query.sql     │
            │               │              │  - update.sql    │
            └───────────────┘              └──────────────────┘
                    │
                    ▼
            ┌───────────────┐
            │  Read-Only    │
            │  Database     │
            │  (for preview)│
            └───────────────┘
```

## Components Breakdown

### 1. UI Components (Frontend)

**New: `WriteScriptSimulator.tsx`**
- Dual-pane layout: Script editor + Results
- SQL editor (Monaco) configured for write SQL
- **"Simulate" button** - Runs SHOWPLAN_XML analysis
- Results display showing:
  - LLM-generated summary at top
  - Execution plan visualization (reuse PlanNode components)
  - Estimated rows affected
  - Optional: Preview of current values
  - Warnings/validations
- **"Create sql-script-runner scripts" button** - Opens form modal

**New: `ScriptGeneratorModal.tsx`**
Form to collect sql-script-runner metadata:
- **Sql Patches repo selection** (dropdown) - Which sql-script-runner repo?
  - Options: "payments-sqlpatches", "transactions-sqlpatches", etc, the list should be configurable.
- **Work Item ID** (text input, required) - Azure DevOps work item number
  - Validation: Must be numeric
  - Creates folder: `SqlPatches/{WorkItemId}/` in the appropriate repo accordeing to the selection above, need to check if the repo exists in the repos base directory which should be configurable. If the repo doesn't exist, close out of the modal and display a message ask the user to pull the repository first. 
- **Purpose/Description** (textarea, required) - Searchable comment for update.sql
  - Placeholder: "Describe what this update does and why..."
- **Expected Affected Rows** (number input, auto-filled from simulation)
  - Pre-populated from SHOWPLAN estimated rows
  - User can override if needed
- **Preview** section showing generated query.sql and update.sql
- **Actions:**
  - "Create Files" button (creates the query and update sql files in a new folder under SqlPatches in the selected erpository)
  - "Copy to Clipboard" button (copies file contents)
  - "Cancel" button

**Updated: `App.tsx`**
- Add tab/toggle for "Write Script Simulator" mode
- Route to simulator component
- Different header/branding to indicate simulation mode

**Styling:**
- Amber/orange theme for simulation pane (vs blue for read queries)
- Warning badges
- "SIMULATION MODE" banner

### 2. Backend Components

**New: `SimulationService.cs`**
```csharp
public interface ISimulationService
{
    Task<SimulationResult> SimulateWriteScriptAsync(WriteScriptRequest request);
}

public class SimulationResult
{
    public bool IsValid { get; set; }
    public string[] ValidationErrors { get; set; }
    public int EstimatedAffectedRows { get; set; }
    public string ExecutionPlanXml { get; set; }  // SHOWPLAN_XML output
    public QueryResult PreviewData { get; set; }   // Optional: Current values via SELECT
    public string[] Warnings { get; set; }
    public string LlmSummary { get; set; }         // LLM-generated plain English summary
    public ForeignKeyValidation[] FkValidations { get; set; }  // Phase 2
}
```

**Implementation Notes:**
- Reuses existing `ExecuteReadOnlyQueryAsync` with `ExecutionPlanMode.Estimated`
- Parses returned execution plan XML (same parser as read queries)
- Optionally runs supplementary SELECT to preview current values
- In Phase 2, adds FK constraint validation queries

**Simulation Strategy:**
Leverage SQL Server's built-in query analysis using `SET SHOWPLAN_XML ON` (Estimated mode):
- **All statement types** (UPDATE/DELETE/INSERT/MERGE): Execute with SHOWPLAN_XML to get execution plan without running the query
- SQL Server's optimizer provides:
  - Estimated rows affected
  - Syntax validation
  - Schema validation (table/column names)
  - Type checking
  - Index usage analysis
  - Execution costs
- **Advantages over manual conversion:**
  - More accurate (uses database optimizer)
  - Catches more errors (syntax, schema, types)
  - Works for complex queries (JOINs, CTEs, multi-table operations)
  - Reuses existing execution plan rendering code
- **Limitation**: Does NOT validate foreign key constraints (requires supplementary queries in Phase 2)

**New: `ScriptGeneratorService.cs`**
```csharp
public interface IScriptGeneratorService
{
    ScriptFiles GenerateScriptFiles(ScriptGenerationRequest request);
}

public class ScriptGenerationRequest
{
    public string WriteScript { get; set; }              // The UPDATE/DELETE/INSERT
    public SimulationResult SimulationResult { get; set; } // From SHOWPLAN analysis
    public string WorkItemId { get; set; }               // Azure DevOps work item
    public string Database { get; set; }                 // Which sql-script-runner repo
    public string Purpose { get; set; }                  // Description for comments
    public int ExpectedAffectedRows { get; set; }        // From simulation or user override
    public string GeneratedBy { get; set; }              // Current user
}

public class ScriptFiles
{
    public string FolderName { get; set; }     // e.g., "12345"
    public string QuerySql { get; set; }       // Content of query.sql
    public string UpdateSql { get; set; }      // Content of update.sql
    public string TargetRepo { get; set; }     // e.g., "sql-script-runner-production"
    public string RelativePath { get; set; }   // "SqlPatches/12345/"
}
```

**Implementation Notes:**
- Generates query.sql with `DECLARE @expectedAffectedRowCount INT = {value};`
- Generates update.sql with rich comments (purpose, issue, affected rows, generated by, database)
- Does NOT include transaction wrapper (sql-script-runner handles this)
- Creates folder structure info (caller decides how to persist: download ZIP, clipboard, or git commit)

### 3. LLM Integration

**New: `SimulatorLlmAssistant.cs`**
- Different system prompt focused on:
  - Helping craft UPDATE/INSERT/DELETE statements
  - Validating write script safety
  - Suggesting WHERE clauses to limit scope
  - Identifying potential unintended consequences
  
**System Prompt Strategy:**
```
You are a SQL write script assistant. Your role is to help users create 
safe, precise UPDATE, INSERT, and DELETE statements to fix data issues.

CRITICAL RULES:
1. Always suggest WHERE clauses to limit scope
2. Warn about potential cascade effects
3. Recommend verification queries (SELECT) before writes
4. Suggest transaction wrappers
5. Highlight foreign key constraints that could fail
6. Never suggest truncating or dropping tables

When the user describes an issue, help them:
1. Query to find affected rows
2. Craft a targeted UPDATE/INSERT/DELETE
3. Preview the changes
4. Generate verification queries
```

**Tools for Simulator LLM:**
- All existing read query tools
- **New: `SimulateWriteScript(sql)`** - Executes query with SHOWPLAN_XML ON, returns execution plan XML and estimated rows
- **New: `InterpretExecutionPlan(planXml)`** - Parses execution plan and generates human-readable summary
- **New: `PreviewAffectedRows(sql)`** - Converts write query to SELECT to show current values (optional)
- **New: `ValidateForeignKeys(sql)`** - (Phase 2) Checks FK constraints that would be violated
- **New: `GenerateVerificationQuery(writeScript)`** - Creates SELECT to run after execution to verify changes

### 4. Simulation Engine Logic

**Core Approach: Use SQL Server's Estimated Plan (SHOWPLAN_XML)**

All write statements use the same simulation technique:

```sql
-- User's intended write script (any of UPDATE/DELETE/INSERT/MERGE):
UPDATE Accounts 
SET Status = 'Active' 
WHERE AccountID IN (15, 38, 39) 
  AND PartnerID = 6;

-- Simulation executes with SHOWPLAN_XML:
SET SHOWPLAN_XML ON;
GO

UPDATE Accounts 
SET Status = 'Active' 
WHERE AccountID IN (15, 38, 39) 
  AND PartnerID = 6;

GO
SET SHOWPLAN_XML OFF;

-- Returns execution plan XML showing:
-- ✓ Estimated rows affected
-- ✓ Clustered Index Seek on Accounts (PK_Accounts)
-- ✓ Query cost: 0.12
-- ✓ Any syntax/schema/type errors
```

**LLM Interpretation Layer:**

The LLM parses the execution plan XML and generates a human-readable summary:

```
📊 Simulation Results:

This UPDATE would affect approximately 3 rows in the Accounts table.

Operation Breakdown:
• Clustered Index Seek on Accounts (PK_Accounts) - Cost: 0.12
• Estimated rows to update: 3
• No table scans (efficient query)

What would change:
• 3 accounts (IDs: 15, 38, 39) would have Status changed to 'Active'
• Partner filter applied: PartnerID = 6

⚠️ Note: Foreign key constraints are NOT validated in estimated mode.
Run supplementary validation queries to check constraints.
```

**Preview Current Values (Optional Supplementary Query):**

To show "before" state, we can still run a SELECT:

```sql
-- Preview what would be affected:
SELECT AccountID, Status AS CurrentStatus, 'Active' AS NewStatus
FROM Accounts 
WHERE AccountID IN (15, 38, 39) 
  AND PartnerID = 6;
```

**Foreign Key Constraint Validation (Phase 2):**

Since SHOWPLAN doesn't validate FK constraints, add supplementary checks:

```sql
-- For DELETE: Check if rows have dependent records
SELECT 'Would fail due to FK constraint' AS Warning, COUNT(*) AS DependentRows
FROM Deposits d
WHERE d.AccountID IN (15, 38, 39)
HAVING COUNT(*) > 0;

-- For UPDATE/INSERT: Validate foreign key references exist
SELECT 'Invalid PartnerID' AS Warning
WHERE NOT EXISTS (SELECT 1 FROM Partners WHERE PartnerID = 6);
```

### 5. Script Output Format (sql-script-runner specification)

**Folder Structure:**
```
sql-script-runner-{database}/
└── SqlPatches/
    └── {WorkItemId}/          # Azure DevOps work item ID (e.g., "12345")
        ├── query.sql          # Verification query
        └── update.sql         # Write script
```

**query.sql** (verification query showing rows that WILL be changed):
```sql
-- Verification Query for Work Item #12345
-- Purpose: Verify suspended accounts linked to active partners
-- Expected Result: 3 rows will be changed
-- Generated: 2026-02-27 08:13:00 UTC

DECLARE @expectedAffectedRowCount INT = 3;

-- Show rows that will be affected by the update
SELECT 
    AccountID, 
    Status AS CurrentStatus, 
    'Active' AS NewStatus,
    PartnerID
FROM Accounts
WHERE AccountID IN (15, 38, 39)
  AND Status = 'Suspended'
  AND PartnerID IN (SELECT PartnerID FROM Partners WHERE Status = 'Active');

-- Return expected count for sql-script-runner validation
SELECT @expectedAffectedRowCount AS ExpectedAffectedRowCount;
```

**update.sql** (the actual write script):
```sql
-- Data Fix Script for Work Item #12345
-- Purpose: Fix suspended accounts that should be active due to their partners being active
-- Issue: Accounts 15, 38, 39 are marked as Suspended but their partner (ID 6) is Active
-- This creates inconsistent state preventing deposits from being processed
-- Generated: 2026-02-27 08:13:00 UTC
-- Generated By: user@example.com
-- Database: production-sql-server
-- Estimated Affected Rows: 3

-- Fix: Set account status to Active to match partner status
UPDATE Accounts 
SET Status = 'Active', 
    UpdatedAt = GETUTCDATE()
WHERE AccountID IN (15, 38, 39)
  AND Status = 'Suspended'
  AND PartnerID IN (SELECT PartnerID FROM Partners WHERE Status = 'Active');

-- Verify the fix
SELECT @@ROWCOUNT AS ActualRowsAffected;
```

**CRITICAL:** The sql-script-runner tool will:
1. Execute query.sql to get `@expectedAffectedRowCount`
2. Execute update.sql within a transaction
3. Compare `@@ROWCOUNT` from update.sql to expected count
4. **ROLLBACK if counts don't match** (safety mechanism)
5. COMMIT only if counts match exactly

**No metadata.json required** - All metadata embedded in SQL comments for searchability

### 6. Git Integration / File Output

**Phase 1: Manual Download (MVP)**
- Generate ZIP file with folder structure: `{WorkItemId}/query.sql` and `{WorkItemId}/update.sql`
- User manually extracts to their sql-script-runner repo clone
- User commits and pushes to repo
- Alternative: Copy to clipboard for quick paste into existing files

**Phase 3: Automated Git Integration (Future)**
```csharp
public interface IScriptRepositoryService
{
    Task<CommitResult> CommitToRepoAsync(ScriptFiles files, string repoPath);
}
```

Options for automation:
- LibGit2Sharp for local git operations
- GitHub API / Azure DevOps API for direct commits
- Create branch + pull request automatically

**Repo Configuration:**
Store sql-script-runner repo paths in appsettings:
```json
{
  "SqlScriptRunner": {
    "Repositories": {
      "production-sql-server": "/path/to/sql-script-runner-production",
      "analytics-postgres": "/path/to/sql-script-runner-analytics",
      "reporting-mysql": "/path/to/sql-script-runner-reporting"
    }
  }
}
```

### 7. Security & Safety

**Validations:**
- ✅ Parse SQL to ensure it's a write statement (UPDATE/INSERT/DELETE)
- ✅ Reject DROP, TRUNCATE, ALTER statements
- ✅ Warn if no WHERE clause (affects all rows)
- ✅ Require explicit confirmation for >100 affected rows
- ✅ Log all simulations to audit trail
- ✅ Never execute write statements (double-check connection is read-only)

**UI Safeguards:**
- Clear "SIMULATION MODE" banner
- Orange/amber color scheme (danger zone)
- Confirmation dialog before generating scripts
- Preview affected rows BEFORE generating files

## Implementation Phases

### Phase 1: Core Simulation (MVP)
- [ ] Create WriteScriptSimulator UI component with orange/amber theme
- [ ] Build SimulationService using SHOWPLAN_XML (reuse existing Estimated mode code)
- [ ] Display execution plan visualization (reuse existing PlanNode renderer)
- [ ] Add LLM tool to interpret execution plan and generate summary
- [ ] Add Simulator LLM with custom prompt for write script assistance
- [ ] Optional: Preview current values with supplementary SELECT
- [ ] **Create ScriptGeneratorModal form component**
  - [ ] Database dropdown (which sql-script-runner repo)
  - [ ] Work Item ID input with validation
  - [ ] Purpose/description textarea
  - [ ] Expected row count (auto-filled from simulation)
  - [ ] File preview section
- [ ] **Generate files according to sql-script-runner spec**
  - [ ] query.sql with `@expectedAffectedRowCount` declaration
  - [ ] update.sql with rich comments (purpose, issue, searchability)
  - [ ] Create folder structure: `SqlPatches/{WorkItemId}/`
- [ ] **Download as ZIP** with correct folder structure
- [ ] **Copy to clipboard** option for quick paste

### Phase 2: Enhanced Validation
- [ ] Foreign key constraint checking
- [ ] Transaction wrapper generation
- [ ] Complex UPDATE/DELETE simulation (JOINs)
- [ ] Multi-statement script support
- [ ] Rollback script generation

### Phase 3: Git Integration
- [ ] ScriptRepositoryService
- [ ] Commit directly to sql-script-runner repo
- [ ] Pull request creation
- [ ] Approval workflow hooks

### Phase 4: Advanced Features
- [ ] Script history/versioning
- [ ] A/B simulation (compare multiple approaches)
- [ ] Schedule script execution reminders
- [ ] Integration with sql-script-runner API

## Open Questions

1. **sql-script-runner Integration:** ✅ ANSWERED
   - **Repo structure**: Multiple repos (one per database) - must ask user which database
   - **Folder structure**: `SqlPatches/{WorkItemId}/` where WorkItemId is from Azure DevOps
   - **Required files**: `query.sql` and `update.sql`
   - **query.sql format**: Must declare `@expectedAffectedRowCount` variable
   - **Validation**: sql-script-runner rollbacks if actual row count ≠ expected
   - **update.sql format**: Must contain descriptive comment for searchability
   - **UI approach**: Form-based (not chat-based) - "Create sql-script-runner scripts" button

2. **Approval Workflow:**
   - Who approves write scripts?
   - Should there be a review stage in the UI?
   - Integration with GitHub PRs for review?

3. **Scope:**
   - Support stored procedures/functions?
   - Support DDL (CREATE/ALTER table)?
   - Support bulk operations (MERGE, BULK INSERT)?

4. **Transaction Handling:**
   - sql-script-runner handles transactions and rollback
   - Scripts should include descriptive comments
   - Generate both query.sql and update.sql according to spec

## Dependencies

**NuGet Packages:**
- No new major dependencies for Phase 1 (reuses existing SqlQueryExecutor)
- Phase 3: `LibGit2Sharp` or `Octokit` for Git integration

**Frontend:**
- ✅ Existing Monaco editor (already in use)
- ✅ Existing ExecutionPlanView component (reuse for write simulations)
- ✅ Existing PlanNode renderer (shows execution plan tree)
- React state management for simulator mode

**Code Reuse:**
- `SqlQueryExecutor.ExecuteReadOnlyQueryAsync()` with `ExecutionPlanMode.Estimated`
- `executionPlanParser.ts` - Parse SHOWPLAN_XML
- `ExecutionPlanView.tsx` - Display plan visualization
- `PlanNode.tsx` - Render operation tree with icons

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Estimated row counts may differ from actual | Label as "estimated", use SQL Server's own optimizer (more accurate than manual conversion) |
| Users confuse simulation with execution | Strong visual cues, warning banners, orange/amber color scheme, "SIMULATION MODE" banner |
| Foreign key violations not detected by SHOWPLAN | Phase 1: Warn users about limitation. Phase 2: Add supplementary FK validation queries |
| Complex multi-statement scripts | Phase 1: Single statement only. Phase 2: Support batches by running SHOWPLAN on each statement individually |
| Users accidentally execute in wrong tool | Clear labeling: "This is a SIMULATOR - scripts must be executed in sql-script-runner" |

## Success Metrics

- ✅ Users can simulate write scripts safely
- ✅ 90%+ accuracy in affected row predictions
- ✅ Zero accidental write executions
- ✅ Scripts generated are valid and executable in sql-script-runner
- ✅ LLM successfully helps craft safe write scripts

## Next Steps

1. Review plan with stakeholders
2. Gather sql-script-runner requirements
3. Design detailed UI mockups
4. Prototype SimulationService core logic
5. Implement Phase 1 MVP

---

**Status:** 📋 Planning Complete - Ready for Review
**Last Updated:** 2026-02-26
