using System.Text.Json.Serialization;

namespace WhisperHeim.Models;

/// <summary>
/// Bootstrap configuration stored in %APPDATA%\WhisperHeim\bootstrap.json.
/// Contains the pointer to the synced data path and machine-local settings
/// that should not be synced across devices (window position, overlay, audio device).
/// </summary>
public sealed class BootstrapConfig
{
    /// <summary>
    /// Path to the synced data folder. Null or empty means use the default
    /// (%APPDATA%\WhisperHeim\), co-located with the bootstrap config.
    /// </summary>
    [JsonPropertyName("dataPath")]
    public string? DataPath { get; set; }

    /// <summary>Machine-local window size and position persistence.</summary>
    [JsonPropertyName("window")]
    public WindowSettings Window { get; set; } = new();

    /// <summary>Machine-local overlay indicator settings.</summary>
    [JsonPropertyName("overlay")]
    public OverlaySettings Overlay { get; set; } = new();

    /// <summary>Machine-local audio device selection for dictation.</summary>
    [JsonPropertyName("audioDevice")]
    public string? AudioDevice { get; set; }

    /// <summary>
    /// One-shot flag: set to <c>true</c> after the post-TTS-removal cleanup has run
    /// (deleting Pocket TTS models, the voices folder, and the <c>tts</c> block in
    /// <c>settings.json</c>). When <c>false</c> on startup, <see cref="WhisperHeim.Services.Settings.DataPathService.MigrateIfNeeded"/>
    /// performs the cleanup and sets this to <c>true</c>.
    /// </summary>
    [JsonPropertyName("ttsCleanupDone")]
    public bool TtsCleanupDone { get; set; }

    /// <summary>
    /// Machine-local Ollama API endpoint URL. Different machines may run
    /// different Ollama servers, so this setting is not synced.
    /// </summary>
    [JsonPropertyName("ollamaEndpoint")]
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Machine-local Ollama model name (e.g. "qwen2.5:14b"). Different machines
    /// may have different models pulled, so this setting is not synced.
    /// </summary>
    [JsonPropertyName("ollamaModel")]
    public string? OllamaModel { get; set; }

    /// <summary>
    /// Stable machine identifier used to stamp recordings with their origin
    /// machine, so multi-machine deployments sharing a cloud-synced
    /// <see cref="DataPath"/> can coordinate transcription ownership.
    /// Populated on first run from a sanitised <see cref="System.Environment.MachineName"/>
    /// (falling back to a short Guid). Immutable after first generation: changing
    /// it would orphan all recordings on this machine. Never synced.
    /// </summary>
    [JsonPropertyName("machineId")]
    public string? MachineId { get; set; }

    /// <summary>
    /// Machine-local folder that recorded-conversation transcripts are
    /// auto-exported to as Markdown (<c>&lt;recording name&gt;.md</c>) on
    /// transcription completion. Null or empty disables auto-export for
    /// recordings. A filesystem path (drive letters, mount points), like
    /// <see cref="DataPath"/> — not synced. See ADR-0008 and task main-m6x4v.
    /// </summary>
    [JsonPropertyName("recordingsExportFolder")]
    public string? RecordingsExportFolder { get; set; }

    /// <summary>
    /// Machine-local folder that imported voice-message transcripts are
    /// auto-exported to as Markdown on transcription completion. Null or
    /// empty disables auto-export for imports. Independent of
    /// <see cref="RecordingsExportFolder"/> — either can be configured alone.
    /// See ADR-0008 and task main-m6x4v.
    /// </summary>
    [JsonPropertyName("importsExportFolder")]
    public string? ImportsExportFolder { get; set; }
}
