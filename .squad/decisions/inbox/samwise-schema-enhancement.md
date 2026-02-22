### 2026-02-22: Schema Metadata Enhancement — Samwise
**By:** Samwise (Backend)
**What:** Extended schema models and provider to include primary keys, foreign keys, indexes, and column properties (identity, defaults, computed). Added `IndexSchema` and `ForeignKeySchema` classes to Core models. Provider now queries `sys.*` catalog views in addition to `INFORMATION_SCHEMA`.
**Why:** UI treeview needs richer metadata to display table structure, relationships, and column properties. LLM also benefits from knowing PKs/FKs for better query generation.
**Backward-compatible:** All new properties have defaults (empty lists, false, null). Existing consumers (LLM layer, API endpoint) work without changes.
**Files changed:** `SchemaContext.cs` (models), `SchemaMetadataProvider.cs` (data loading).
