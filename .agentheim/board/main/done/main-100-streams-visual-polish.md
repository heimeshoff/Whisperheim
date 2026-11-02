---
id: main-100
title: Streams page visual polish
status: done
type: feature
context: main
created: 
completed: 2026-04-07
commit:
depends_on: []
blocks: []
tags: [ui-polish]
related_adrs: []
related_research: []
prior_art: []
milestone: UI Polish
size: Medium
---
# Streams page visual polish

## Description

The Streams page looks plain compared to the rest of the app and the editorial inspiration design (see `inspiration/youtube/`). Apply visual polish while staying within the existing WPF UI theme — no new design system, just richer layout and better consistency.

## Acceptance Criteria

### Header
- [ ] Larger, bolder page title (~24-28px, matching DictationPage style) instead of tiny 10px section label
- [ ] Descriptive subtitle below the title
- [ ] Fix SectionLabel: use `DynamicResource TextFillColorSecondaryBrush` instead of hardcoded `#FF005FAA`

### URL Input Area
- [ ] Link icon inside the input field
- [ ] Platform indicators below the input (e.g. "YouTube supported · Instagram Reels")
- [ ] Better spacing and container feel

### Transcript Cards — Richer Layout
- [ ] Source platform badge above the title (YouTube / Instagram icon + label)
- [ ] Transcription method badge as pill ("Captions" or "Parakeet ASR")
- [ ] Metadata in labeled columns ("Transcribed" / date, "Duration" / value) instead of inline
- [ ] Action buttons (copy, delete) as icon-only row with hover accent — visible without expanding
- [ ] Better hover state on the whole card

### Section Divider
- [ ] "RECENT TRANSCRIPTS" section label above the card list

### Consistency
- [ ] Match margins/MaxWidth from other pages (40px margins, MaxWidth ~900)
- [ ] Use DynamicResource brushes throughout (no hardcoded colors)

## Notes

- Reference: `inspiration/youtube/DESIGN.md`, `inspiration/youtube/screen.png`, `inspiration/youtube/code.html`
- Keep all existing functionality intact (expand/collapse, copy, delete, progress, batch transcription)
- This is visual-only — no new features or backend changes

## Work Log

### 2026-04-07
- Fixed SectionLabel style: replaced hardcoded `#FF005FAA` with `DynamicResource TextFillColorSecondaryBrush`
- Replaced 10px "STREAMS" label with 28px bold "Streams" title + descriptive subtitle
- Wrapped URL input in a card container with Link24 icon and platform indicators ("YouTube supported · Instagram Reels")
- Added "RECENT TRANSCRIPTS" section label above transcript list
- Changed margins from 24px to 40px, added MaxWidth="900", wrapped in ScrollViewer (matching DictationPage)
- Added TranscriptCard hover state via style trigger (SubtleFillColorSecondaryBrush)
- Added source platform badge (YouTube/Instagram/Video) with icon detection from URL
- Added transcription method pill badge ("Captions" / "Parakeet ASR") using PillBadge style
- Metadata now displayed in labeled columns ("Transcribed" + date, "Duration" + value)
- Copy and delete buttons moved to card header as icon-only buttons with IconActionButton style (hover accent)
- Added DetectPlatform helper method for URL-based platform detection
- All hardcoded colors removed from styles; DynamicResource brushes used throughout
- Build verified: 0 errors
