---
id: main-013
title: Template System
status: done
type: feature
context: main
created: 2026-03-21
completed: 2026-03-21
commit:
depends_on: [main-011]
blocks: []
tags: [m1, dictation]
related_adrs: []
related_research: []
prior_art: []
milestone: M1 - Live Dictation + Core App
size: Medium
---
# Template System

## Objective
Define named text templates triggered by a dedicated hotkey + voice keyword.

## Details
Templates stored in settings.json as a list of {name, text} pairs. Dedicated template hotkey (default: Alt+Win). Workflow: press template hotkey -> say template name -> template text is inserted at cursor. Use ASR to transcribe the spoken name, fuzzy-match against template names. Template text supports multi-line and basic placeholders ({date}, {time}, {clipboard}). Settings UI page for managing templates (add, edit, delete). Show a small toast/notification confirming which template was matched.

## Acceptance Criteria
- [x] Template triggered by hotkey+voice
- [x] Correct template inserted
- [x] Fuzzy matching works for approximate names
- [x] Templates manageable in settings UI
- [x] Confirmation shown

## Notes
Fuzzy matching for voice-to-template-name. Placeholders: {date}, {time}, {clipboard}. Default hotkey: Alt+Win.

## Work Log
<!-- Appended by /work during execution -->

### 2026-03-21 — Implementation complete

**Changes made:**

1. **AppSettings.cs** — Changed `TemplateSettings.Items` from `List<string>` to `List<TemplateItem>` (name/text pairs). Added `TemplateItem` model class.

2. **New: Services/Templates/FuzzyMatcher.cs** — Levenshtein-distance-based fuzzy matching with containment detection. Matches spoken text against template names with configurable threshold (default 0.4).

3. **New: Services/Templates/TemplatePlaceholderExpander.cs** — Expands `{date}` (yyyy-MM-dd), `{time}` (HH:mm), `{clipboard}` (current clipboard text) placeholders in template text.

4. **New: Services/Templates/ITemplateService.cs** — Interface for template matching and CRUD operations.

5. **New: Services/Templates/TemplateService.cs** — Implementation that delegates to FuzzyMatcher and TemplatePlaceholderExpander. Persists changes via SettingsService.

6. **New: Services/Templates/TemplateOrchestrator.cs** — Wires template hotkey -> dictation pipeline -> fuzzy match -> text insertion. Includes 4-second timeout, cancellation on second hotkey press, and tray notification feedback.

7. **TemplatesPage.xaml + .xaml.cs** — Full management UI with ListBox of templates, name/text input fields, and Add/Update/Delete buttons.

8. **MainWindow.xaml.cs** — Registered second `GlobalHotkeyService` for template hotkey (Alt+Win), created `TemplateOrchestrator`, added `ShowTemplateNotification` method, updated cleanup in `OnClosing`.

9. **App.xaml.cs** — Created `TemplateService` and passed it to `MainWindow`.

Build: 0 errors, 0 warnings.
