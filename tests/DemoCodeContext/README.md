# Demo Code Context Project

This is a demo Entity Framework Core project that matches the seeded test database schema. Use this to test the Code Context system with the LLM.

## Schema

This project contains 8 entities matching the cash deposit platform database:

1. **Partner** - Partner banks and financial institutions
2. **User** - Platform operators and staff
3. **DepositLocation** - Physical locations (branches, ATMs, kiosks)
4. **Account** - Customer accounts linked to partners
5. **Deposit** - Individual deposit transactions
6. **Fee** - Fee structures per partner and deposit type
7. **Reconciliation** - Deposit reconciliation records
8. **AuditLog** - Audit trail of all operations

## Testing with LLM

Start the application and point it at this directory:

### Configuration
Add to `appsettings.Development.json`:

```json
{
  "CodeContext": {
    "DefaultRepositoryPath": "D:\\git\\sql-audited-query-tool\\tests\\DemoCodeContext",
    "AllowedDirectories": [
      "D:\\git\\sql-audited-query-tool\\tests"
    ]
  }
}
```

### Example Questions

**Discovery:**
- "What entities are in the tests\\DemoCodeContext directory?"
- "List all files in tests\\DemoCodeContext\\Entities"
- "Show me all DbContext classes in DemoCodeContext"

**Entity Analysis:**
- "What properties does the Account entity have?"
- "Show me the Deposit entity with all its annotations"
- "What navigation properties does Partner have?"

**Relationships:**
- "How are Account and Partner related?"
- "What foreign keys does Deposit have?"
- "Show me all relationships in the Deposit entity"

**Code Search:**
- "Find all [Required] attributes in DemoCodeContext"
- "Search for MaxLength annotations"
- "Find ForeignKey attributes"

**Fluent API:**
- "What Fluent API configurations exist in CashDepositDbContext?"
- "What indexes are configured on Deposit?"
- "Show me the OnModelCreating method"
