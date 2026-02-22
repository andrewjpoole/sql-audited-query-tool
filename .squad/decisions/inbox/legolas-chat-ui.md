### 2026-02-22T16:00:00Z: Read vs Fix Query UI Separation — Legolas
**By:** Legolas (Frontend)
**What:** Read query suggestions from the LLM show a green "Insert & Execute" button that auto-runs the query. Fix query suggestions show a yellow/orange warning banner reading "⚠️ FIX QUERY — Must be run in a separate tool with write access" with only an "Insert into Editor" button (no execute). This is determined by the `isFixQuery` boolean on the `QuerySuggestion` type from the API.
**Why:** Core security requirement — the tool only has read-only database access. Fix queries must never be executed through this tool. The visual distinction (color + banner + button absence) makes it impossible for users to accidentally run a write query.
**Impact:** Backend (`/api/chat`, `/api/query/suggest`) must return `isFixQuery: boolean` on suggestion responses. LLM prompt engineering must classify suggestions correctly.
