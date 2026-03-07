### 2026-07-24: SSE Streaming & Thinking Content — Frontend UI Pattern
**By:** Legolas (Frontend Dev)
**What:** ChatPanel now handles `text` and `thinking` SSE events for real-time streaming display.
- `text` events accumulate incrementally (append, not replace) and update the assistant bubble progressively
- `thinking` events show in a collapsible `<details>` section with animated indicator, auto-collapses when response text begins
- Streaming state uses local accumulator variables + React setState to avoid stale closures
- Added `thinking` to `StreamEvent` type union in queryApi.ts
**Why:** Users need liveness feedback during LLM inference. Thinking content provides transparency into model reasoning without cluttering the response.
**Contract:** Backend sends `{ "type": "thinking", "content": "..." }` and `{ "type": "text", "content": "..." }` SSE events. `done` event finalizes as before.
**Files:** ChatPanel.tsx, ChatPanel.css, queryApi.ts
