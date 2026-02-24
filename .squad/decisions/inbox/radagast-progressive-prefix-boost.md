# Decision: Progressive Prefix Boost for Autocomplete

**Date:** 2025-01-XX  
**Decider:** Radagast (LLM Engineer)  
**Status:** Implemented

## Context

Users reported that autocomplete suggestions were disappearing as they typed more characters:
- Typing "s" → SELECT appears in suggestions
- Typing "se" → SELECT **disappears** (wrong!)
- Expected: Each additional matching character should make SELECT MORE prominent, not less

## Problem

The prefix matching boost in `EmbeddingCompletionService` was **constant** regardless of match length:
- "s" matching "SELECT" → +500 points
- "se" matching "SELECT" → +500 points (same!)
- "sel" matching "SELECT" → +500 points (same!)

When combined with variable semantic similarity scores from embeddings, this caused:
- Longer prefixes could score LOWER than shorter ones
- Items would randomly appear/disappear as user typed
- Poor user experience and confusing autocomplete behavior

## Decision

Implemented **progressive prefix boosting** that scales with match length:

```csharp
var matchLength = wordAtCursor.Length;
var lengthRatio = (float)matchLength / displayText.Length;
var basePrefixBoost = isKeyword ? 500.0f : 100.0f;
var progressiveBoost = basePrefixBoost * (1.0f + lengthRatio * 2.0f);
baseScore += progressiveBoost;
```

**Example for "SELECT" (keyword, 7 chars):**
- "s" (1 char): 500 × (1.0 + 0.14 × 2.0) = 643 pts
- "se" (2 chars): 500 × (1.0 + 0.29 × 2.0) = 786 pts ✓ HIGHER
- "sel" (3 chars): 500 × (1.0 + 0.43 × 2.0) = 929 pts ✓ HIGHER
- "sele" (4 chars): 500 × (1.0 + 0.57 × 2.0) = 1071 pts ✓ HIGHER

## Consequences

**Positive:**
- Autocomplete is now deterministic and predictable
- Each additional character typed increases confidence/rank
- Users can progressively narrow down suggestions by typing more
- Fixes the critical UX bug where items vanish as you type

**Negative:**
- Slightly more complex scoring formula
- Need to ensure lengthRatio doesn't cause division issues (handled: displayText.Length always > 0)

## Alternatives Considered

1. **Use only exact match boost:** Would miss partial matches during typing
2. **Increase base boost value:** Doesn't solve the regression problem, just masks it
3. **Remove semantic similarity entirely:** Loses the intelligence of embedding-based search

## Implementation

**File Modified:** `src/SqlAuditedQueryTool.Llm/Services/EmbeddingCompletionService.cs`  
**Lines Changed:** 79-89  
**Testing:** Build successful, logic verified with mathematical examples
