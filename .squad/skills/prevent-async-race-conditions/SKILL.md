# Skill: Prevent React Async Race Conditions

## When to Use
When you need to prevent duplicate execution of async operations in React components (e.g., API calls, query executions, form submissions).

## Problem
React state updates are asynchronous and batched. If an async operation checks state to prevent duplicates, multiple rapid triggers can all pass the check before the state update takes effect, causing race conditions.

## Anti-Pattern ❌
```typescript
const [loading, setLoading] = useState(false);

const handleExecute = async () => {
  if (loading) return;  // Race condition: multiple calls can pass this check
  setLoading(true);     // State update is async
  await executeQuery(); // Multiple executions run in parallel
  setLoading(false);
};
```

## Solution ✅
Use `useRef` to track execution state synchronously:

```typescript
const [loading, setLoading] = useState(false);
const executingRef = useRef(false);

const handleExecute = async () => {
  if (executingRef.current) return; // Synchronous check - prevents race
  
  executingRef.current = true;      // Immediate synchronous update
  setLoading(true);                 // UI state for loading indicator
  
  try {
    await executeQuery();
  } finally {
    setLoading(false);
    executingRef.current = false;   // Always reset in finally
  }
};
```

## Why It Works
- `useRef` updates are **synchronous** and immediate
- Second call sees `executingRef.current = true` before any async work starts
- `useState` is kept for UI rendering (loading spinners, disabled buttons)
- `useRef` is used for execution guard logic

## When Applied
- **Frontend:** `App.tsx` lines 28, 55-89, 90-123, 129-163
- **Functions:** `handleExecute`, `handleExecuteSelection`, `handleInsertAndExecute`
- **Fixed:** Multiple query execution errors when user presses Execute (F7) rapidly

## Key Principles
1. Use refs for synchronous execution guards
2. Keep state for UI rendering purposes
3. Always reset the ref in a `finally` block
4. Check the ref first, set it immediately before async work

## Related Patterns
- Debouncing/throttling (for timing-based prevention)
- Abort controllers (for canceling in-flight requests)
- Request deduplication (for identical concurrent requests)
