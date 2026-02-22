# Decision: Schema TreeView Context Menu Pattern

**By:** Legolas (Frontend Dev)  
**Date:** 2026-02-22T20:30:00Z  
**Status:** Implemented

## What
Added right-click context menu to SchemaTreeView table rows with three common SQL query patterns:
- SELECT TOP 1000 * FROM [schema].[table]
- SELECT COUNT(*) FROM [schema].[table]
- SELECT * FROM [schema].[table] WHERE (leaves cursor after WHERE)

## How
- Context menu state (visibility, position, target table) managed with `useState`
- `useRef` + `useEffect` for click-outside detection to close menu
- `onContextMenu` handler on table rows prevents default browser menu
- Menu positioned at click coordinates using fixed positioning
- CSS styled with dark theme variables, consistent with existing UI

## Why
- **UX improvement:** Right-click is natural for power users wanting quick query templates
- **Discovery:** Makes common query patterns discoverable without documentation
- **Efficiency:** Faster than typing full queries or using multiple clicks
- **Pattern:** Context menus are expected UI in database tools (SSMS, Azure Data Studio, etc.)

## Constraints
- Only appears on table rows, not on columns/indexes/FKs (avoid menu overload)
- Uses existing `onInsertText` callback — no new prop API needed
- Qualified table names respect schema (dbo vs non-dbo)
- Menu closes on selection or click-outside (standard UX)
- Fixed z-index 9999 ensures menu appears above all other UI

## Future Considerations
- Could add column-level context menu for column-specific queries (SELECT col, WHERE col =, GROUP BY col)
- Could add "Copy table name" option for clipboard access
- Could make menu items configurable via user preferences
- Could add keyboard navigation (arrow keys + Enter)
