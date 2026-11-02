---
id: main-112
title: Public README + GitHub Release Page Content
status: done
type: feature
context: main
created: 2026-05-12
completed: 2026-05-12
commit:
depends_on: [main-111]
blocks: []
tags: [m5, release]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - Public Release (GitHub Distribution)
size: Medium
---
# Public README + GitHub Release Page Content

## Objective

Produce the public-facing content a first-time visitor needs to download, install, and use WhisperHeim from GitHub Releases. Cover: SmartScreen click-through, Smart App Control caveat, SHA-256 verification, optional `winget install Gyan.FFmpeg` step, hotkeys, and where the user's data lives.

## Details

### 1. README.md (top-level, replaces or augments any existing internal-only README)

Structure:

- **Hero**: one paragraph — "WhisperHeim is the audio Swiss army knife for Windows 11. Local dictation, call transcription with speaker separation, voice message transcription. No cloud, no subscription, no internet at runtime."
- **Screenshot / screen-recording** of dictation in action.
- **Download** — direct link to the latest Release's `Setup.exe`.
- **Install** — SmartScreen click-through (numbered steps + screenshot/recording).
  - Click "More info" → "Run anyway" if "Windows protected your PC" appears.
  - If "Smart App Control" blocks instead, we cannot override (no signing yet). State that signing is planned post-UG.
- **First run** — what to expect:
  - The setup window (Task 108) downloads ~640 MB Parakeet model from Hugging Face. One-time, then cached in `%APPDATA%\WhisperHeim\models`.
  - Tray icon appears; right-click for menu.
  - Default hotkeys (Ctrl+Win to dictate, etc.).
- **Optional: FFmpeg** — "For YouTube/Stream transcription, install FFmpeg: `winget install -e --id Gyan.FFmpeg`. WhisperHeim will prompt you the first time you need it."
- **Where is my data?** — `%APPDATA%\WhisperHeim` for settings/recordings/transcripts by default. Configurable via the General settings page. Uninstall does NOT delete this data.
- **System requirements** — Windows 11 (24H2 or later recommended), x64, ~2 GB RAM idle / ~3 GB during transcription, ~700 MB disk for the Parakeet model.
- **Privacy** — bullet list: all transcription is local, no telemetry, no cloud accounts.
- **License** — TBD (the licensing decision comes out of the monetization research; placeholder for now).
- **Status / disclaimer** — "Currently unsigned because Microsoft Trusted Signing isn't available to individual developers in Germany yet. Signing is planned post-UG registration. SHA-256 of every Setup.exe is published in the Release notes for verification."

### 2. GitHub Release notes template

Each release should include:
- **What changed** — bullet list (manual or auto-generated from commits).
- **Install** — link back to README install section + brief reminder of SmartScreen click-through.
- **Verification** — `SHA-256: <hash from Task 111 workflow output>`.
- **Known issues** — anything explicit.

Store this as `.github/release-template.md` so the workflow (or a manual step) can pre-fill it. Velopack's `vpk upload --releaseNotes` flag can consume this.

### 3. Screen recording

A 20–30 s `install.mp4` (or animated `.webp` / `.gif`) showing the click-through. Host at the repo root or in a `docs/media/` folder. Linked from the README install section.

### 4. SHA-256 surfacing

Task 111's workflow emits the Setup.exe SHA-256 as a step output. Either:
- Append it to the Release body automatically via `gh release edit` in the same workflow, or
- Include it manually in the Release notes from the workflow output.

Choose whichever is simpler at implementation time — the goal is "user can find the hash without grepping logs".

### 5. SmartScreen + SAC explainer

A short FAQ-style section (in README or a `docs/why-unsigned.md`):
- What is SmartScreen?
- Why is WhisperHeim flagged?
- How can you verify the download is safe? (SHA-256, source code, no telemetry.)
- What is Smart App Control and why might it block the install entirely?
- When will WhisperHeim be signed? (Post-UG; see roadmap.)

## Acceptance Criteria

- [ ] Top-level `README.md` covers: hero, screenshots, download, install (SmartScreen), first-run, optional FFmpeg, data location, requirements, privacy, license placeholder, unsigned disclaimer
- [ ] `install.mp4` (or `.webp`/`.gif`) recorded and linked from README
- [ ] `.github/release-template.md` exists with placeholders for changelog + SHA-256
- [ ] SHA-256 from Task 111 workflow shows up in Release body (automated or manual, documented in `docs/release.md`)
- [ ] `docs/why-unsigned.md` or equivalent section explains SmartScreen and SAC with clear next-steps for the user
- [ ] Optional FFmpeg `winget` line present in README install section
- [ ] User-tested: a friend / colleague who hasn't seen WhisperHeim can install it from the GitHub Release with no extra hand-holding

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §5 "SmartScreen + GitHub Releases UX" + Implications #6.
- Related research: `.workflow/research/macwhisper-growth-playbook.md` — the README and Release page are part of the launch surface; should at minimum read as well as MacWhisper's.
- Don't write "WhisperHeim respects your privacy" without backing it up — the data-location section, the no-telemetry claim, and the open-source nature of the dependencies are the proof.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-12 14:49 — Work Completed

**What was done:**
- Rewrote the top-level `README.md` to be the public-facing entry point (hero, download link, SmartScreen install steps, first-run expectations, default hotkeys, optional FFmpeg, data location with uninstall-preservation note matching Task 113, system requirements, privacy bullets, signing/verification, source-build instructions, Ko-fi, TBD license placeholder). Preserved the project's pragmatic-technical tone; kept the existing Ko-fi block and a "Build from source" subsection so the README still serves contributors.
- Created `docs/why-unsigned.md` covering: what SmartScreen is, why a new unsigned binary is flagged, three independent verification paths (SHA-256, source, network), what Smart App Control does and why it hard-blocks, the signing roadmap (UG → Microsoft Trusted Signing → flip the workflow flag), and what users can do in the meantime.
- Created `.github/release-template.md` with four placeholders (`{{VERSION}}`, `{{SETUP_NAME}}`, `{{SHA256}}`, `{{CHANGES}}`) plus install/verification/FFmpeg/known-issues/notes sections matching the README's tone. Designed to be consumable by either a manual paste or `vpk upload --releaseNotes` / `gh release edit --notes-file`.
- Appended a "Surfacing the SHA-256 in the Release body" section to `docs/release.md` with both the manual workflow (copy from job summary into the template) and a `gh release edit`-based automation sketch. Added Task 112 to the Related-tasks list so the cross-link is bidirectional.
- Created `docs/media/` with a placeholder README documenting exactly what `install.mp4` and the hero recording should contain, with the size budget and capture steps so a future recording session is a paint-by-numbers job. Left `<!-- TODO -->` comments in `README.md` at the two spots where the media will plug in — did NOT fake a recorded artifact, per task instructions.

**Acceptance criteria status:**
- [x] Top-level `README.md` covers all required sections — hero paragraph (line 3-5), download link to `releases/latest` with `certutil` hint, install with SmartScreen + Smart App Control branches, first-run (Parakeet download + tray + bundled VAD/Seg), default hotkeys table, optional FFmpeg with `winget` line, data location with uninstall-preservation note cross-linked to Task 113's behaviour, system requirements, privacy bullets (local, no telemetry, no accounts, outbound traffic enumerated), signing/verification with `docs/why-unsigned.md` link, TBD license placeholder pointing at roadmap.
- [ ] `install.mp4` (or `.webp` / `.gif`) recorded and linked from README — **DEFERRED**. Subagents cannot record screen captures. Placeholders left in README (`<!-- TODO -->` comments referencing `docs/media/install.mp4`); `docs/media/README.md` documents the exact recording recipe. Manual follow-up before the first public Release announcement.
- [x] `.github/release-template.md` exists with placeholders for changelog + SHA-256 — created with four named placeholders and explanatory HTML comments at the top.
- [x] SHA-256 from Task 111 workflow shows up in Release body — documented the path end-to-end in `docs/release.md` (manual default + automation sketch). The workflow's `SHA-256 of Setup.exe` step already emits to the job summary; the new docs section closes the loop from summary → user-visible Release body.
- [x] `docs/why-unsigned.md` explains SmartScreen and SAC with clear next-steps — separate file per task body, covers both Defender SmartScreen (More info → Run anyway) and Smart App Control (hard block, only "fix" is to disable SAC or wait), plus the signing roadmap.
- [x] Optional FFmpeg `winget` line present in README install section — section titled "Optional: install FFmpeg" with `winget install -e --id Gyan.FFmpeg`.
- [ ] User-tested: a friend / colleague who hasn't seen WhisperHeim can install it from the GitHub Release with no extra hand-holding — **DEFERRED**. Cannot be performed from a subagent. Recommend running this manual check during Task 114's E2E dry run with a non-technical observer.

**Files changed:**
- `README.md` — full rewrite to public-release shape; kept Ko-fi and source-build sections.
- `docs/why-unsigned.md` — new file; SmartScreen / SAC / signing-roadmap FAQ.
- `.github/release-template.md` — new file; release-body template with four placeholders.
- `docs/media/README.md` — new file; placeholder + recording recipe for `install.mp4` and the hero clip.
- `docs/release.md` — appended "Surfacing the SHA-256 in the Release body" section (manual + `gh release edit` automation sketch) and added Task 112 to the Related-tasks list.

**Deferred manual follow-ups (carried in the README / docs as TODO comments):**
- Record `docs/media/install.mp4` (and a hero clip) before the first public Release announcement. Recipe in `docs/media/README.md`.
- User-test the README with someone who hasn't seen WhisperHeim — fold into Task 114's manual checklist.
