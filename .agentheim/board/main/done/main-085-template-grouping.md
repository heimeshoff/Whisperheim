---
id: main-085
title: Template Grouping with Collapsible Sections
status: done
type: feature
context: main
created: 2026-03-26
completed: 2026-03-26
commit:
depends_on: []
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Large
---
# Template Grouping with Collapsible Sections

## Description

Add user-defined grouping to templates, mirroring the collapsible group UX from the Transcripts page. Also add collapse/expand-all toggle to both Templates and Transcripts pages.

## Acceptance Criteria

### Template Groups — Data Model & Persistence
- [ ] Groups stored in `settings.json` alongside existing template data
- [ ] Each group has: name, ordered list of template references, isExpanded state
- [ ] Collapse/expand state persists across sessions
- [ ] "Ungrouped" group always exists, is first, cannot be deleted or renamed
- [ ] Templates belong to exactly one group (no multi-group linking)

### Template Groups — UI
- [ ] Groups displayed as collapsible sections with chevron toggle, group name, count badge
- [ ] Custom group names are editable inline (click to rename)
- [ ] Each group header row has a "+" button to add a new template directly into that group
- [ ] Existing "Add New" button repurposed to create new groups instead
- [ ] Empty groups show a delete icon on hover of the group name
- [ ] Non-empty groups do not show the delete icon
- [ ] Custom groups are reorderable via drag-and-drop
- [ ] Templates are drag-and-droppable between groups (move, not copy)
- [ ] Search/filter works across all groups, auto-expands groups containing matches

### Collapse/Expand All
- [ ] Add collapse/expand-all toggle button to Templates page
- [ ] Add collapse/expand-all toggle button to Transcripts page

## Technical Notes

### Key Files
- **Models:** `src/WhisperHeim/Models/AppSettings.cs` — Add `TemplateGroup` model, extend `TemplateSettings`
- **Service:** `src/WhisperHeim/Services/Templates/TemplateService.cs` / `ITemplateService.cs` — Group CRUD operations
- **Settings:** `src/WhisperHeim/Services/Settings/SettingsService.cs` — Persistence
- **Templates UI:** `src/WhisperHeim/Views/Pages/TemplatesPage.xaml` + `.cs` — Major rework for grouped layout
- **Transcripts UI:** `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` + `.cs` — Add expand/collapse all button

### Reference Pattern
- `TranscriptGroupViewModel` in `TranscriptsPage.xaml.cs` provides the proven collapsible group pattern (chevron, expand state preservation, count display)

### Migration
- Existing templates with no group assignment should automatically land in "Ungrouped" on first load after upgrade

## Notes
- Conversation (transcript) groups remain auto-ordered by date, not user-reorderable
- Template group ordering is user-controlled via drag-and-drop
- "Ungrouped" is a system group — always first, not deletable, not renameable

### 2026-03-26 12:00 -- Work Completed

**What was done:**
- Added `TemplateGroup` model and `Group` property on `TemplateItem` in `AppSettings.cs`
- Extended `ITemplateService` and `TemplateService` with full group CRUD (add, rename, delete, reorder, expand/collapse state persistence, move template between groups)
- Added `EnsureDefaults()` migration: creates "Ungrouped" group on first load; templates with null group are treated as ungrouped
- Reworked `TemplatesPage.xaml` and `.xaml.cs` to display templates in collapsible groups with chevron toggle, count badge, group-specific "+" add button, delete icon for empty custom groups
- Repurposed "ADD NEW" button to "NEW GROUP" for creating template groups
- Implemented drag-and-drop for templates between groups (move, not copy) and group reordering
- Custom group names are renameable via double-click (opens InputDialog)
- Search/filter works across all groups, auto-expands groups with matches
- Added expand/collapse-all toggle button to Templates page
- Added expand/collapse-all toggle button to Transcripts page
- Created `InputDialog.xaml` and `.xaml.cs` for text input (group naming/renaming)

**Acceptance criteria status:**
- [x] Groups stored in `settings.json` alongside existing template data -- `TemplateGroup` list in `TemplateSettings`
- [x] Each group has: name, ordered list of template references, isExpanded state -- via `TemplateGroup` model + `TemplateItem.Group` reference
- [x] Collapse/expand state persists across sessions -- `SetGroupExpanded` saves to settings
- [x] "Ungrouped" group always exists, is first, cannot be deleted or renamed -- enforced in `EnsureDefaults`, `RemoveGroup`, `RenameGroup`
- [x] Templates belong to exactly one group -- `TemplateItem.Group` is a single string reference
- [x] Groups displayed as collapsible sections with chevron toggle, group name, count badge -- XAML template mirrors TranscriptsPage pattern
- [x] Custom group names are editable inline (click to rename) -- double-click opens InputDialog
- [x] Each group header row has a "+" button to add a new template directly into that group -- `AddTemplateToGroup_Click` handler
- [x] Existing "Add New" button repurposed to create new groups instead -- now "NEW GROUP" button
- [x] Empty groups show a delete icon on hover of the group name -- `ShowDeleteIcon` property
- [x] Non-empty groups do not show the delete icon -- `ShowDeleteIcon` returns false when items > 0
- [x] Custom groups are reorderable via drag-and-drop -- `TemplateGroupDrag` DnD implemented
- [x] Templates are drag-and-droppable between groups (move, not copy) -- `TemplateDisplayItem` DnD implemented
- [x] Search/filter works across all groups, auto-expands groups containing matches -- in `RefreshList()`
- [x] Add collapse/expand-all toggle button to Templates page -- `ExpandCollapseAll_Click`
- [x] Add collapse/expand-all toggle button to Transcripts page -- `ExpandCollapseAll_Click`

**Files changed:**
- `src/WhisperHeim/Models/AppSettings.cs` -- Added `TemplateGroup` class, `Group` property on `TemplateItem`, `Groups` list on `TemplateSettings`
- `src/WhisperHeim/Services/Templates/ITemplateService.cs` -- Added group CRUD methods to interface
- `src/WhisperHeim/Services/Templates/TemplateService.cs` -- Implemented group CRUD, migration, expand/collapse persistence
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml` -- Major rework for grouped layout with collapsible sections, DnD, expand/collapse all
- `src/WhisperHeim/Views/Pages/TemplatesPage.xaml.cs` -- Rewritten for grouped display, DnD, inline rename, expand/collapse all
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml` -- Added expand/collapse-all toggle button
- `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs` -- Added `ExpandCollapseAll_Click` handler
- `src/WhisperHeim/Views/InputDialog.xaml` -- New dialog for text input (group naming)
- `src/WhisperHeim/Views/InputDialog.xaml.cs` -- New code-behind for InputDialog
