---
id: main-t9w2k
title: Inline template creation dialog when no template matches
status: todo
type: feature
context: main
created: 2026-06-29
completed:
depends_on: []
blocks: []
tags: [templates, ui, dialog]
related_adrs: []
related_research: []
prior_art: [main-013]
---

## Why
When the templating shortcut is triggered and the spoken word matches no
template, the app currently shows a small bottom-right toast ("No template
match for: …") and does nothing else. That dead-ends the user: the toast just
reports failure, then disappears.

The maintainer wants the *opposite* — a no-match should be an invitation to
create the missing template on the spot. That way templates can be used
optimistically ("assume it exists") and any miss is resolved immediately,
without breaking flow to go to the Dictation/Templates page and build it by
hand. It also covers the common case where the term was simply *misheard* —
the user can fix the trigger word in the same dialog.

## What
Replace the no-match toast with a **centered modal dialog** that lets the user
create the missing template inline.

Current wiring (for the worker):
- `DictationOrchestrator` fires `TemplateNoMatch(rawText)` when
  `TemplateService.MatchAndExpand(rawText)` returns null
  (`src/WhisperHeim/Services/Orchestration/DictationOrchestrator.cs:63,328`).
- `App.xaml.cs:504-505` currently handles that event with
  `ToastWindow.Show($"No template match for: \"{spokenText}\"")`.

The change: for the no-match case, show the new dialog instead of the toast.

The dialog:
- Appears centered on screen (reuse the existing centered-modal pattern —
  `Views/InputDialog.xaml` / `DeleteConfirmationDialog.xaml`, plain `Window` +
  `ShowDialog`; there is no WPF-UI `ContentDialog` in the app).
- States plainly that no template was found for the spoken term and that one
  can be created now.
- **Two editable fields**, mapping to the existing `TemplateItem` model
  (`Models/AppSettings.cs:105-118`):
  - **Term / trigger name** — pre-filled with the transcribed `rawText`, editable
    so a mishearing can be corrected (→ `TemplateItem.Name`).
  - **Replacement text** — multiline text area for what the template expands to
    (→ `TemplateItem.Text`). Empty by default.
- A **Create** action that persists the new template via the same path the
  start-page drawer uses — `TemplateService.AddTemplate(name, text, group)`
  (`Services/Templates/TemplateService.cs:96`) — and a **Cancel** that closes
  without creating anything.
- **Save-only behavior** (decided with the user 2026-06-29): creating the
  template does *not* immediately type its text into the previously-focused
  app. The template simply exists for next time. No re-focus / SendInput on
  create.

## Acceptance criteria
- [ ] Triggering the templating shortcut with a word that matches no template
      shows a **centered modal dialog**, not the bottom-right `ToastWindow`.
- [ ] The dialog clearly communicates "no template found for «term» — create
      one now".
- [ ] The dialog has an editable **term/trigger** field, pre-filled with the
      transcribed spoken text, so a misheard term can be corrected before saving.
- [ ] The dialog has an editable multiline **replacement text** field for the
      template body.
- [ ] Confirming creates a new user template via `TemplateService.AddTemplate`
      with the (possibly edited) term as `Name` and the entered body as `Text`;
      it is persisted to settings exactly as a start-page-created template is,
      and appears in the Dictation/Templates list afterward.
- [ ] Saving with an empty term is prevented (no nameless template); behavior
      on empty body matches the start-page drawer's rule.
- [ ] Cancel / Escape closes the dialog and creates nothing.
- [ ] After creating, the new template's text is **not** auto-inserted into the
      focused app (save-only).
- [ ] The old "No template match for: …" toast is no longer shown for the
      no-match case. (`ToastWindow` may remain for other uses.)

## Notes
- Prior art: **main-013** (Template System) established `TemplateItem`,
  `TemplateService`, and the fuzzy `MatchAndExpand` flow this builds on.
- The start-page drawer also supports a **group** (`TemplateItem.Group`,
  null = "Ungrouped"). For the inline dialog the simplest correct default is to
  create **ungrouped**; a group picker is optional polish, not required by the
  ACs. Match the start-page drawer's field set as closely as is reasonable
  without bloating the dialog.
- Focus note: a modal `ShowDialog` will take focus from the target app while
  open. That is fine here (the user is actively typing the template) and,
  given save-only behavior, there is no need to restore focus to the target
  window on create.
- Open question deferred to the worker if it surfaces: whether the dialog
  should be dismissible the same way the dictation overlay is, or behave as a
  standard top-most modal. Default to a standard centered top-most modal
  consistent with `InputDialog`.
