---
id: main-088
title: System Templates — WhisperHeim Group with "Repeat" Command
status: done
type: feature
context: main
created: 
completed: 2026-03-26
commit:
depends_on: [main-085]
blocks: []
tags: []
related_adrs: []
related_research: []
prior_art: []
size: Medium
---
# System Templates — WhisperHeim Group with "Repeat" Command

## Description

Add a special "WhisperHeim" group to the templates list that contains system-level command templates. These are immutable, built-in templates that provide control functionality rather than text expansion. The first system template is "Repeat," which re-types the last normally-dictated text.

## Acceptance Criteria

### WhisperHeim Group (UI)
- [ ] "WhisperHeim" group always appears at the bottom of the template group list, after all user groups
- [ ] Group is collapsible like other groups (chevron, persisted expand/collapse state)
- [ ] Group cannot be renamed or deleted
- [ ] No "Add template" (+) button shown on the group header
- [ ] No drag-and-drop into or out of the WhisperHeim group (templates and group itself)
- [ ] Group header cannot be dragged to reorder

### System Template Items (UI)
- [ ] Displayed with reduced contrast (subtler text color) compared to regular templates
- [ ] Not clickable — no edit drawer opens on click
- [ ] Not editable, not deletable, not movable
- [ ] Two-column display: Name | Description (description explains what the command does)
- [ ] No drag initiation from system template rows

### "Repeat" Template
- [ ] Name: "Repeat"
- [ ] Description: "Types the last dictated text again"
- [ ] When triggered via template hotkey (Ctrl+Win+Alt), re-types the last text that was dictated in normal (non-template) mode
- [ ] If no previous dictation exists, does nothing (or shows a toast indicating nothing to repeat)
- [ ] Fuzzy matching works the same as regular templates (user says "repeat" → matches "Repeat")

### System Integration
- [ ] System templates are not persisted in settings.json — they are defined in code
- [ ] System templates participate in the same fuzzy matching as user templates during template mode
- [ ] System templates take precedence if a user template has the same name (or warn/prevent user from creating one with a conflicting name)

## Implementation Notes

- Define system templates as a static/hardcoded collection (not in `AppSettings`)
- The `DictationOrchestrator` already handles template mode — extend it to store the last normal dictation result for "Repeat"
- `TemplateService.MatchAndExpand()` needs to check system templates alongside user templates
- In `DictationPage.xaml.cs`, the WhisperHeim group display model needs flags to suppress drag, click, edit, and add behaviors
- Use `TextFillColorSecondaryBrush` or `TextFillColorTertiaryBrush` for the reduced-contrast text

## Files to Modify

- `src/WhisperHeim/Models/AppSettings.cs` — possibly add a `IsSystem` concept or keep system templates separate
- `src/WhisperHeim/Services/Templates/TemplateService.cs` — register system templates, merge into matching
- `src/WhisperHeim/Services/Templates/ITemplateService.cs` — expose system templates
- `src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs` — store last normal dictation result, handle "Repeat" action
- `src/WhisperHeim/Views/Pages/DictationPage.xaml` — WhisperHeim group styling, reduced contrast, non-clickable rows
- `src/WhisperHeim/Views/Pages/DictationPage.xaml.cs` — suppress drag/click/edit for system templates

## Work Log

### 2026-03-26 — Implementation Complete

**Changes made:**

1. **New file: `src/WhisperHeim/Models/SystemTemplate.cs`** — `SystemTemplate` model class and `SystemTemplateDefinitions` static registry with the "Repeat" template (action ID `system.repeat`). Defines the `SystemGroupName` constant ("WhisperHeim").

2. **`ITemplateService.cs`** — Added `GetSystemTemplates()` method. Extended `TemplateMatchResult` record with `IsSystemTemplate` and `SystemActionId` properties.

3. **`TemplateService.cs`** — `MatchAndExpand()` now merges system templates (with precedence) into fuzzy matching. System templates return a result with `IsSystemTemplate=true` and the action ID. Added guards to prevent renaming/deleting/reordering the WhisperHeim group. System group always sorts last in `GetGroups()`.

4. **`DictationOrchestrator.cs`** — Stores `_lastNormalDictation` after each normal dictation. Added `HandleSystemAction()` which dispatches on action ID; the `system.repeat` action re-types the last dictated text. Does nothing if no prior dictation exists.

5. **`DictationPage.xaml`** — Add (+) button visibility now bound to `ShowAddButton` (hidden for system groups). Template row DataTemplate uses DataTriggers to apply `TextFillColorTertiaryBrush` and normal font weight for system template items. Cursor changes to Arrow for system rows.

6. **`DictationPage.xaml.cs`** — `TemplateGroupDisplayModel` extended with `IsSystem`, `ShowAddButton`, `AllowGroupDrag` properties. `TemplateDisplayItem` extended with `IsSystem` flag and a constructor for `SystemTemplate`. `RefreshTemplateList()` appends the WhisperHeim group at the bottom with system template items. All event handlers (click, drag, rename, delete, drop) guard against system templates/groups.

**All acceptance criteria met:**
- WhisperHeim group appears at bottom, collapsible, not renameable/deletable/draggable, no add button
- System template items displayed with reduced contrast, not clickable/editable/draggable
- "Repeat" template defined with correct name/description, triggers re-typing last normal dictation
- System templates defined in code (not settings), participate in fuzzy matching with precedence
- Build succeeds with zero errors
