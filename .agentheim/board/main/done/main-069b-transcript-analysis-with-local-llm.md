---
id: main-069
title: Transcript Analysis with Local LLM
status: done
type: feature
context: main
created: 
completed: 2026-03-26
commit:
depends_on: []
blocks: []
tags: [call-transcription]
related_adrs: []
related_research: []
prior_art: []
milestone: Milestone 2 (Audio Capture + Call Transcription)
size: Medium
---
# Transcript Analysis with Local LLM

## Description

Add AI-powered analysis of recorded transcripts using a local LLM via Ollama. Users can define reusable prompt templates (e.g., "Action Items", "Key Decisions", "Ideas", "Meeting Summary") and run them against any recording's transcript. Results stream in real-time.

This avoids cloud API costs entirely — the user runs Ollama locally with an open-source model like Qwen 2.5 14B, consistent with WhisperHeim's local-first, zero-cost philosophy.

## Research

See `.workflow/research/transcript-analysis-with-llm.md` for full findings.

- Claude subscription cannot be used programmatically (blocked by Anthropic since Jan 2026)
- Ollama + Qwen 2.5 14B is the best local alternative (~80-90% of GPT-4 quality for extraction tasks)
- OllamaSharp NuGet provides `IChatClient` via Microsoft.Extensions.AI
- 1.5h transcripts (~18K tokens) fit in Qwen 2.5's 128K context window without chunking

## Implementation Outline

### 1. Ollama Integration Layer
- Add **OllamaSharp** NuGet package
- Service class wrapping `OllamaApiClient` with `IChatClient`
- Connection test / health check via Ollama's `/api/tags` endpoint
- Streaming response support

### 2. Settings
- Ollama endpoint URL (default `http://localhost:11434`)
- Model selector — auto-detect installed models via `/api/tags`
- Connection status indicator

### 3. Prompt Templates
- User-defined prompts stored in app settings or a JSON file
- Each template has a **title** and a **prompt body** (with `{transcript}` placeholder)
- Ship with a few built-in defaults: Action Items, Key Decisions, Ideas, Meeting Summary
- UI to create, edit, and delete templates

### 4. Analysis UI on Transcripts Page
- "Analyze" button/dropdown on selected recording
- Shows available prompt templates
- Selecting one sends the full Markdown transcript + prompt to Ollama
- Response streams into a result panel (Markdown rendered)
- Option to copy result or save alongside the recording

## Acceptance Criteria

- [x] OllamaSharp integrated, connection to local Ollama verified
- [x] Settings page allows configuring Ollama URL and selecting a model
- [x] User can create, edit, and delete prompt templates
- [x] Built-in default templates ship with the app
- [x] "Analyze" action available on transcripts page for any recording
- [x] Analysis result streams in real-time
- [x] Works fully offline with no cloud dependency
- [x] Graceful error handling when Ollama is not running or model not installed

## Files to Modify/Create

- New: `Services/OllamaService.cs` (or similar — Ollama integration)
- New: `Models/PromptTemplate.cs` (template data model)
- Modify: `Views/Pages/TranscriptsPage.xaml` + `.xaml.cs` (add Analyze UI)
- Modify: Settings page (Ollama configuration)
- Modify: App settings / config (template storage, Ollama settings)

## Work Log

### 2026-03-26 — Implementation Complete

**Changes made:**

1. **Added OllamaSharp NuGet package** (v5.4.25) to `WhisperHeim.csproj`
2. **Created `Models/AnalysisPromptTemplate.cs`** — data model for analysis prompt templates with Id, Title, Prompt, and IsBuiltIn properties
3. **Added `OllamaSettings` class to `Models/AppSettings.cs`** — stores endpoint URL, selected model, and analysis templates in persisted settings
4. **Created `Services/Analysis/OllamaService.cs`** — Ollama integration service with:
   - Connection testing via `TestConnectionAsync()`
   - Model auto-detection via `ListLocalModelsAsync()`
   - Streaming analysis via `AnalyzeAsync()` using `IChatClient` from Microsoft.Extensions.AI
   - Template CRUD (add, update, delete with built-in protection)
   - 4 built-in default templates: Action Items, Key Decisions, Ideas, Meeting Summary
5. **Updated `Views/Pages/GeneralPage.xaml` + `.xaml.cs`** — added "AI Analysis (Ollama)" settings section with:
   - Endpoint URL configuration with Test button and status indicator
   - Model selector dropdown with Refresh button (auto-detects installed models)
   - Setup instructions for users
6. **Updated `Views/Pages/TranscriptsPage.xaml` + `.xaml.cs`** — added analysis UI:
   - "ANALYZE" button in the drawer action bar (blue accent, BrainCircuit icon)
   - Context menu dropdown listing all prompt templates + Custom Prompt option
   - Analysis results panel with streaming text display, auto-scroll, title, status
   - Stop/Cancel button during streaming, Copy button after completion, Close button
   - Custom prompt dialog for ad-hoc analysis
7. **Updated `App.xaml.cs`** — creates OllamaService and passes it through
8. **Updated `MainWindow.xaml.cs`** — accepts OllamaService, passes to GeneralPage and TranscriptsPage

**Build:** Successful (0 errors, only pre-existing warnings)
