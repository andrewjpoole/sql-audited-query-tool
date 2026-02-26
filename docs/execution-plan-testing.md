# Execution Plan Feature - Manual Test Script

## Backend Testing

### 1. Test with IncludeExecutionPlan = false (default)
```bash
curl -X POST http://localhost:5000/api/query/execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT TOP 10 * FROM Users",
    "source": "User",
    "includeExecutionPlan": false
  }'
```

**Expected Response:**
- `executionPlanXml`: `null`
- Normal result sets returned
- No SQL wrapping

### 2. Test with IncludeExecutionPlan = true
```bash
curl -X POST http://localhost:5000/api/query/execute \
  -H "Content-Type: application/json" \
  -d '{
    "sql": "SELECT TOP 10 * FROM Users",
    "source": "User",
    "includeExecutionPlan": true
  }'
```

**Expected Response:**
- `executionPlanXml`: Contains XML starting with `<ShowPlanXML xmlns=...>`
- Normal result sets returned (WITHOUT the plan result set)
- SQL was wrapped with `SET STATISTICS XML ON/OFF`

### 3. Verify Query History includes flag
```bash
curl http://localhost:5000/api/query/history?limit=5
```

**Expected Response:**
- Recent queries show `includedExecutionPlan: true/false` based on request

## Verification Checklist

- [ ] Build succeeds: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] Database tests pass: 6/6 new tests
- [ ] API accepts `includeExecutionPlan` parameter
- [ ] API returns `executionPlanXml` when requested
- [ ] Plan XML starts with `<ShowPlanXML`
- [ ] Plan result set excluded from `resultSets` array
- [ ] History stores `includedExecutionPlan` flag
- [ ] Default behavior unchanged (flag defaults to false)

## Sample Execution Plan XML Structure

```xml
<ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.564">
  <BatchSequence>
    <Batch>
      <Statements>
        <StmtSimple StatementText="SELECT TOP 10 * FROM Users" ...>
          <QueryPlan>
            <RelOp NodeId="0" PhysicalOp="Top" EstimateRows="10" ...>
              <Top RowCount="10">
                <RelOp NodeId="1" PhysicalOp="Clustered Index Scan" ...>
                  ...
                </RelOp>
              </Top>
            </RelOp>
          </QueryPlan>
        </StmtSimple>
      </Statements>
    </Batch>
  </BatchSequence>
</ShowPlanXML>
```

## Integration with Frontend

Once Legolas completes the frontend implementation:

1. **Checkbox** in toolbar sends `includeExecutionPlan: true/false`
2. **ExecutionPlanView** component receives `executionPlanXml` string
3. **html-query-plan** library renders the visual plan
4. **User sees** interactive execution plan diagram in dedicated tab

---

**Status:** ✅ Backend ready for frontend integration
