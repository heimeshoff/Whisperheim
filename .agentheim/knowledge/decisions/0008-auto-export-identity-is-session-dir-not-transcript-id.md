---
id: 0008
title: Auto-export identity is the Recording-session directory, not CallTranscript.Id
scope: main
status: accepted
date: 2026-07-09
supersedes: []
superseded_by: []
related_tasks: [main-m6x4v]
related_research: []
---

# ADR 0008: Auto-export identity is the Recording-session directory, not CallTranscript.Id

## Context
The auto-export feature (settings-configured default export folders for recorded
conversations and imported voice messages; on transcription, write `<recording
name>.md` into the configured folder) needs an "overwrite on re-transcription"
rule that doesn't silently clobber an unrelated recording that happens to share
a title.

`CallTranscript.Id` cannot serve as that identity: `CallTranscriptionPipeline
.ProcessAsync` (`CallTranscriptionPipeline.cs:85-86`) generates a **fresh**
`transcriptId` (`{timestamp}_{new Guid}`) on every transcription, including
re-transcriptions of the same recording. What *is* stable across
re-transcriptions is the Recording-session directory
(`recordings/<timestamp>_<machineId>/`), and `CallTranscript.Name` (the
editable title) survives re-transcription by construction — `ReTranscribe_Click`
(`TranscriptsPage.xaml.cs:2358/2380`) carries `transcript.Name` into the new
session's title.

Renaming a recording (`SaveTranscriptNameAsync`, `TranscriptsPage.xaml.cs
:1471-1517`) has no event/seam that anything else observes — it's a direct
`UpdateAsync` write with no notification. Chasing renames into the export
folder would require inventing that seam and would risk deleting a file the
user has since edited or moved out-of-band — the same conservatism the
feature's "missing folder → skip silently" decision already applies elsewhere.

## Decision
Auto-export identity is the **Recording-session directory**, tracked via a new
persisted field `CallTranscript.ExportedMarkdownPath` (serialized in
`transcript.json`, rewritten via the existing `ITranscriptStorageService
.UpdateAsync`). On each auto-export:

- Compute `desired = <export folder>/<sanitize(Name)>.md`.
- If `ExportedMarkdownPath` already equals `desired`, **overwrite in place**
  (re-transcription of the same, unrenamed recording).
- Otherwise, disambiguate `desired` with numeric suffixes (`name (2).md`, …)
  until a free path is found, so a same-titled sibling recording never
  overwrites another session's file — ownership is decided from *this*
  session's own persisted memory, never by inspecting the existing file.
- Persist the resulting path back onto `ExportedMarkdownPath`.

A rename **without** re-transcription does not touch the export: the old
`<old-name>.md` is **left orphaned** (not renamed, not deleted) until the next
re-transcription writes a new `<new-name>.md` and updates
`ExportedMarkdownPath`. Auto-export is a transcription-time snapshot, not a
live mirror.

## Consequences
### Positive
- Re-transcription is idempotent at the file level (same file overwritten).
- Two recordings that happen to share a title never clobber each other's
  exported Markdown.
- No new storage machinery — reuses the existing `transcript.json` read/write
  path (`LoadAsync`/`UpdateAsync`).

### Negative / Neutral
- Renaming a recording after it has been auto-exported leaves a stale
  `<old-name>.md` behind until the next re-transcription. This is a known,
  accepted gap — documented so it isn't mistaken for a bug.
- Requires reloading `CallTranscript` from disk in the export subscriber
  (the queue item does not carry the built transcript for Recording-type
  items) rather than reusing an in-memory object.

## Alternatives considered
- **Filename `<name>_<id-or-timestamp>.md`.** Collision-proof by construction,
  but rejected — contradicts the builder's explicit filename requirement
  (`<recording name>.md`) and produces uglier names than intended.
- **Re-derive ownership by scanning the export folder for a file whose content
  embeds the session id.** Rejected — requires parsing/round-tripping a marker
  through the exported `.md` body (fragile if the user edits the file) instead
  of tracking ownership on the aggregate that already owns it.
- **Chase renames into the export folder (rename/delete the old `.md` on
  rename).** Rejected — no existing event seam on rename, and deleting a file
  the user may have since edited outside the app is destructive; contradicts
  the feature's existing conservative "never destroy on uncertainty" stance
  (missing-folder skip is silent, not a hard failure).

## References
- Code: `src/WhisperHeim/Services/CallTranscription/CallTranscriptionPipeline.cs:85-86`
  (fresh `transcriptId` per transcription), `src/WhisperHeim/Views/Pages/TranscriptsPage.xaml.cs:2293-2386`
  (`ReTranscribe_Click` — Name survives re-transcription), `:1471-1517`
  (`SaveTranscriptNameAsync` — no notification seam on rename)
- Storage: `src/WhisperHeim/Services/CallTranscription/TranscriptStorageService.cs`
  (`LoadAsync`/`UpdateAsync`)
- Related BC: `main` (single core BC — see `.agentheim/contexts/main/README.md`)
