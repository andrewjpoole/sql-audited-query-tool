### 2026-02-22T14:15:00Z: React Frontend Architecture — Legolas
**By:** Legolas (Frontend Dev)
**What:** Created React SPA inside `src/SqlAuditedQueryTool.App/ClientApp/` using Vite + React + TypeScript with Monaco SQL editor.
**Key choices:**
- Vite 7 + React 19 + TypeScript (stable, not Vite 8 beta)
- `@monaco-editor/react` for SQL editing with dark theme
- 6 custom context menu commands in "SQL Helpers" group (Insert Date, GUID, GETDATE(), NEWID(), Wrap in SELECT, Toggle Comment)
- Plain CSS with custom properties (no CSS framework — keeps bundle small)
- SPA served via `Microsoft.AspNetCore.SpaServices.Extensions` v9.x
- Vite dev server on port 5173 proxies `/api` calls to .NET on port 5001
- `GET /api/health` endpoint added for proxy/connectivity testing
**Why:** Monaco provides a professional code editing experience with built-in SQL syntax highlighting. Context menu commands make common SQL patterns (dates, GUIDs, wrapping) discoverable and fast. Vite ensures fast dev iteration. SPA middleware keeps frontend and backend in one deployable unit.
**Constraints:**
- SPA package must be pinned to 9.x for net9.0 compatibility (10.x requires net10.0)
- Monaco `addAction` run callback type is `ICodeEditor`, not `IStandaloneCodeEditor`
