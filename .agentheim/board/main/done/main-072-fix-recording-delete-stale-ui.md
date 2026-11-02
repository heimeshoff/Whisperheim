---
id: main-072
title: Fix Recording Delete Not Removing List Item
status: done
type: bug
context: main
created: 
completed: 2026-03-24
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
milestone: --
size: Small
---
# Fix Recording Delete Not Removing List Item

## Description

When deleting a recording, the list item remains visible in the UI even though the files are deleted on disk. Navigating to another tab and back does not clear the stale entry either — only a full app restart removes it. The UI state (or the data source feeding it) is not being refreshed after a successful delete.

## Why

Broken UX — users see ghost entries for recordings they already deleted, making the app feel buggy and untrustworthy.

## Acceptance Criteria

- [x] After deleting a recording, the entry immediately disappears from the list
- [x] No stale entries remain after navigating away and back
- [x] Other list items are unaffected

## Technical Notes

- Likely a missing state update or event after the delete operation completes
- Check whether the recordings list is driven by a cached collection that isn't being invalidated
- May also need to verify the same pattern isn't broken for template deletion

## Work Log

### 2026-03-24

Found and fixed three related issues in `TranscriptsPage.xaml.cs`:

1. **Transcript delete: UI refresh was inside try block.** If `Directory.Delete` threw (e.g., file handle not yet released on Windows), the `LoadTranscriptList()` call was skipped entirely. Fixed by moving the disk-delete into its own try/catch and always removing the item from `_allItems` + calling `ApplyFilter()` afterward, ensuring the entry vanishes from the UI regardless of whether the disk operation succeeded.

2. **Pending recording delete: same pattern.** The `LoadTranscriptList()` call was inside the try block after `Directory.Delete`. Moved it outside so the UI always refreshes.

3. **Navigation back to Recordings tab showed stale list.** The page was cached in `_pageCache` and only loaded its transcript list once in the constructor. Added a `Loaded` event handler that calls `LoadTranscriptList()` each time the page becomes visible, ensuring the list is always fresh when navigating back from another tab. Removed the redundant constructor call to avoid double-loading.

Template deletion was already correct (uses a different pattern with in-memory collection).
