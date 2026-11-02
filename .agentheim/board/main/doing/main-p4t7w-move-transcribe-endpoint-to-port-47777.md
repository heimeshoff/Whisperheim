---
id: main-p4t7w
title: Move the loopback transcribe endpoint's default port from 7777 to 47777 (server, CLI, Claude Code plugin shim, docs, ADR-0001) so it stops clashing with games and dev tools
status: doing
type: chore
context: main
created: 2026-11-02
completed:
depends_on: []
blocks: []
tags: [transcribe-endpoint, http, port, cli, claude-code-plugin]
related_adrs: [0001]
related_research: []
prior_art: [main-h7k2p, main-q4m8t]
---

## Why
The transcribe HTTP endpoint (ADR-0001) binds `http://127.0.0.1:7777/` by default. 7777 is a
popular port: games (e.g. Terraria and many dedicated game servers) and common dev tools grab
it, so the bind fails or another process answers instead. A high port such as **47777** is
very unlikely to clash. WhisperHeim owns this endpoint, so the fix is to move the default, not
to make the user override it every time.

## What
Change the default port of the loopback transcribe endpoint from `7777` to `47777` everywhere
it is hard-coded, and keep the existing override knobs exactly as they are
(`WHISPERHEIM_TRANSCRIBE_PORT` on the app side, `WHISPERHEIM_ENDPOINT` /`-Endpoint` on the
caller side). Loopback-only binding, wire contract and non-fatal-bind behavior are unchanged.

Known places that hard-code 7777 (from a grep on 2026-11-02):
- `src/WhisperHeim/Services/Http/TranscribeServer.cs` — `DefaultPort = 7777` + XML doc comment.
- `src/WhisperHeim.Cli/CliCore.cs` — `DefaultEndpoint = "http://127.0.0.1:7777"`.
- `tests/WhisperHeim.Cli.Tests/CliCoreTests.cs` — default-endpoint and URL-building assertions.
- `claude-code-plugin/scripts/transcribe-lib.ps1` — `WhisperHeimDefaultEndpoint`.
- `claude-code-plugin/scripts/transcribe.ps1` — header comment.
- `claude-code-plugin/tests/transcribe-lib.Tests.ps1` — default-endpoint and URL assertions.
- `claude-code-plugin/commands/transcribe.md` and `claude-code-plugin/README.md` — user-facing text.
- `.agentheim/knowledge/decisions/0001-transcribe-endpoint-loopback-http.md` — the default port
  in the Decision section (amend it: new default 47777, plus a dated note on why it moved).
  Leave done task files as they are, since they are historical records.

## Acceptance criteria
- [ ] `TranscribeServer.DefaultPort == 47777`; with `WHISPERHEIM_TRANSCRIBE_PORT` unset the
      server binds `http://127.0.0.1:47777/`, and the env var still overrides it.
- [ ] `CliCore.DefaultEndpoint == "http://127.0.0.1:47777"`; the CLI tests assert the new default
      and pass (`dotnet test`).
- [ ] The plugin shim's default endpoint is `http://127.0.0.1:47777`; the Pester tests in
      `claude-code-plugin/tests/` assert the new default and pass.
- [ ] `claude-code-plugin/.claude-plugin/plugin.json` version is bumped (0.1.0 → 0.1.1) so
      installed copies pick up the new default when updated.
- [ ] `git grep -n "7777"` outside `.agentheim/board/**/done/` and `.workflow/` shows no
      remaining reference to the old port (except ADR-0001's dated "moved from 7777" note).
- [ ] ADR-0001 says the default is 47777 and records why it moved.

## Notes
- **Callers outside this repo can't be changed by a worker.** The builder has to update these by
  hand after deploying: any hotkey/AutoHotkey scripts, OBS or Stream Deck actions, other tools
  that call `http://127.0.0.1:7777`, any `WHISPERHEIM_ENDPOINT` env var set to the old port,
  and the installed `whisperheim-transcribe` plugin copy under
  `~/.claude/plugins/cache/whisperheim-transcribe/` (it still says 7777 until the plugin is
  updated). The worker should list these in its outcome as a reminder.
- Utterheim uses 7223. 47777 doesn't collide with it, so ADR-0001's "two apps coexist" point
  still holds.
- Consider whether `/health` or the bind-failure log line should name the port. It probably
  already does; check, and don't add scope.
