# Chat History UI Redesign

**Date:** 2026-02-22  
**By:** Legolas (Frontend Dev)  
**Status:** Implemented

## Problem
The chat history sidebar implementation had critical issues:
1. Infinite loop causing flickering when clicking "Add new"
2. Empty/no history displayed
3. Sidebar design didn't match user requirements

## Root Cause
The infinite loop was caused by a `useEffect` dependency on `onMessagesChange` callback:
- `ChatPanel` had `useEffect(() => { onMessagesChange(messages) }, [messages, onMessagesChange])`
- `onMessagesChange` callback was recreated on every parent render
- New callback reference triggered effect → state update → parent re-render → new callback → infinite cycle

## Solution

### Architecture Change
- **Removed:** Separate `ChatSessionList` sidebar component and toolbar button
- **Added:** Integrated chat selector at the TOP of `ChatPanel` component
- **New UI:** Collapsible "Chats (N)" section with expandable session list

### State Management Fix
- **Eliminated problematic useEffect patterns**
- ChatPanel now receives sessions array directly from parent
- Uses `useCallback` for `updateMessages` to ensure stable reference
- Parent (`App.tsx`) owns all session state via `useChatHistory` hook

### Data Flow (Before → After)
**Before:**
```
App.tsx manages sessions → ChatPanel receives sessionId + initialMessages
ChatPanel maintains local messages state → syncs via onMessagesChange callback
useEffect syncs on session change → useEffect notifies parent on message change (LOOP!)
```

**After:**
```
App.tsx manages sessions via useChatHistory hook
App.tsx passes sessions array + currentSessionId + callbacks to ChatPanel
ChatPanel reads messages from sessions array (single source of truth)
ChatPanel updates via onUpdateSession callback (stable via useCallback)
```

### UI Design
- **Position:** Top of ChatPanel (not sidebar)
- **Collapsed state:** Shows "▶ Chats (N)" + "✚ New" button (minimal space)
- **Expanded state:** Shows scrollable list (max 200px) with sessions
- **Each session shows:** Title (40 char preview), timestamp (relative), message count, delete button
- **Active highlight:** Current session has blue background tint
- **Delete confirmation:** Requires user confirmation to prevent accidents

## Impact
- ✅ Fixed infinite loop/flickering bug
- ✅ Chat history now displays correctly from localStorage
- ✅ More compact UI (no sidebar, integrated into chat panel)
- ✅ Better UX (collapsible, always accessible from chat panel)
- ✅ Cleaner state management (single source of truth)

## Files Changed
- `ChatPanel.tsx` - Integrated session list, removed local state sync
- `ChatPanel.css` - Added styles for `.chat-sessions-*` classes
- `App.tsx` - Removed ChatSessionList sidebar, simplified callbacks
- `history.md` - Documented learnings about useEffect dependency pitfalls

## Key Learning
**Avoid callback functions in useEffect dependencies unless they're wrapped in useCallback.**  
Prefer direct props and single source of truth over syncing state between parent/child with effects.
