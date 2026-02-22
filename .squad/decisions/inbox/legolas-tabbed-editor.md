# Tabbed Query Editor Implementation

**Date:** 2026-02-23T10:00:00Z  
**By:** Legolas (Frontend)  
**Status:** Implemented

## Decision

Implemented a tabbed interface for SQL queries using Monaco Editor's native multi-model support via the `path` prop pattern.

## Context

Andrew requested a tabbed interface for managing multiple SQL query files/buffers. The question was whether Monaco Editor has native tab support or if we need to build custom tab management.

## Research Findings

Monaco Editor **does not** have a built-in tab UI component. However, `@monaco-editor/react` v4.7.0+ provides native **multi-model** support through the `path` prop:

- When you provide a `path` prop, Monaco creates and manages separate models automatically
- Each model maintains independent state: content, view state, undo stack, scroll position, cursor position
- Switching between paths is handled by Monaco internally — no manual model management needed
- This is the same pattern VS Code uses for its tab system

Reference: https://github.com/suren-atoyan/monaco-react#multi-model-editor

## Implementation

### Component Architecture
- Created `TabbedSqlEditor.tsx` to replace `SqlEditor.tsx`
- Tab bar UI built in React (custom component)
- Monaco multi-model API handles editor state (native support)

### Tab Features
- **Create new tabs:** "+" button generates new query with unique path
- **Close tabs:** "×" button with dirty state warnings (must keep ≥1 tab)
- **Switch tabs:** Click to activate; Monaco switches models automatically
- **Rename tabs:** Double-click tab name to rename
- **Dirty tracking:** "●" indicator shows unsaved changes per tab
- **Unique paths:** Each tab has unique path like `query-1.sql`, `query-2.sql` for Monaco model management

### Technical Pattern
```typescript
<Editor
  path={activeTab.path}           // Monaco creates/retrieves model by path
  defaultValue={activeTab.defaultValue}  // Only used on first model creation
  onChange={handleEditorChange}
  // ... other props
/>
```

## Why This Approach?

1. **Native Monaco support:** Leverages Monaco's built-in multi-model capability — no manual model lifecycle management
2. **Battle-tested pattern:** Same approach VS Code uses for tabs
3. **State preservation:** View state, undo/redo, selections persist across tab switches automatically
4. **Performance:** Monaco optimizes model storage and switching internally
5. **Minimal custom code:** Only UI (tab bar) is custom; editor state is Monaco's responsibility

## Integration Notes

- `App.tsx` updated to import `TabbedSqlEditor` instead of `SqlEditor`
- Same `SqlEditorHandle` ref interface maintained for backward compatibility
- All existing features (context menus, keyboard shortcuts, schema insertion) work unchanged
- Query history, chat panel, results display integration unchanged

## User Experience

- **Tab bar:** Horizontal tabs with scroll overflow, VS Code-style visual design
- **Active tab:** Highlighted with accent color border
- **Dirty state:** Visual "●" indicator per tab
- **Keyboard-friendly:** Can extend with Ctrl+N for new tab, Ctrl+W for close tab in future
- **Confirmation dialogs:** Warns before closing tabs with unsaved changes

## Future Enhancements (Out of Scope)

- Keyboard shortcuts (Ctrl+N new tab, Ctrl+W close tab, Ctrl+Tab switch)
- Save/open query files from disk
- Drag-to-reorder tabs
- Split editor view for side-by-side queries
- Pin tabs to prevent accidental closure

## Files Changed

- **Created:** `src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.tsx`
- **Created:** `src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.css`
- **Modified:** `src/SqlAuditedQueryTool.App/ClientApp/src/App.tsx` (import change)
- **Kept:** `src/components/SqlEditor.tsx` (may deprecate later, but left intact for reference)

## Build Verification

✅ `npm run build` succeeds (75 modules, 233.47KB JS gzip 73.24KB)

## Recommendation

This implementation provides a solid foundation for multi-query workflows. The use of Monaco's native multi-model support ensures stability and performance. Future keyboard shortcuts can be added incrementally without architectural changes.
