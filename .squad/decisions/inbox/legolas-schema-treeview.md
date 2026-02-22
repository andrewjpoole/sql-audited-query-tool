### 2026-02-22T18:00:00Z: Schema TreeView — Frontend API Contract

**By:** Legolas (Frontend)
**What:** Updated TypeScript types in `queryApi.ts` to expect richer schema data from `GET /api/schema`. The backend response must now include:
- `SchemaTable.primaryKey: string[]` — column names in the PK
- `SchemaTable.indexes: SchemaIndex[]` — each with `name`, `columns`, `isUnique`, `isClustered`
- `SchemaTable.foreignKeys: SchemaForeignKey[]` — each with `name`, `columns`, `referencedSchema`, `referencedTable`, `referencedColumns`
- `SchemaColumn` fields: `isPrimaryKey`, `isIdentity`, `defaultValue`, `isComputed`

**Why:** The schema treeview needs this data to display keys, indexes, FK relationships, and column metadata. Backend (Samwise) needs to match this contract.

**Also:** `SqlEditor` now exposes `SqlEditorHandle` via `forwardRef`/`useImperativeHandle` so parent components can insert text at the Monaco cursor position. Schema panel is always-visible (not toggled) but collapsible.
