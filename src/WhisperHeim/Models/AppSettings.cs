using System.Text.Json.Serialization;

namespace WhisperHeim.Models;

/// <summary>
/// Application settings persisted as JSON in %APPDATA%/WhisperHeim/settings.json.
/// </summary>
public sealed class AppSettings
{
    /// <summary>General settings.</summary>
    [JsonPropertyName("general")]
    public GeneralSettings General { get; set; } = new();

    /// <summary>Dictation / transcription settings.</summary>
    [JsonPropertyName("dictation")]
    public DictationSettings Dictation { get; set; } = new();

    /// <summary>Template settings.</summary>
    [JsonPropertyName("templates")]
    public TemplateSettings Templates { get; set; } = new();

    /// <summary>Overlay indicator settings.</summary>
    [JsonPropertyName("overlay")]
    public OverlaySettings Overlay { get; set; } = new();

    /// <summary>Ollama / LLM analysis settings.</summary>
    [JsonPropertyName("ollama")]
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>Window size and position persistence.</summary>
    [JsonPropertyName("window")]
    public WindowSettings Window { get; set; } = new();
}

public sealed class GeneralSettings
{
    /// <summary>Launch WhisperHeim when Windows starts.</summary>
    [JsonPropertyName("launchAtStartup")]
    public bool LaunchAtStartup { get; set; }

    /// <summary>Start minimized to the system tray.</summary>
    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; set; } = true;

    /// <summary>Application theme: "Dark", "Light", or "System".</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Light";

    /// <summary>
    /// Default speaker name for the local user in call transcripts.
    /// When set, replaces "You" as the label for mic audio segments.
    /// Falls back to "You" if empty or null.
    /// </summary>
    [JsonPropertyName("defaultSpeakerName")]
    public string? DefaultSpeakerName { get; set; }

    /// <summary>
    /// Machine-local: keep the Parakeet recognizer resident for the process
    /// lifetime instead of idle-unloading it after 5 minutes. Mirrored from
    /// <see cref="WhisperHeim.Models.BootstrapConfig.KeepModelLoaded"/> by
    /// <see cref="WhisperHeim.Services.Settings.SettingsService"/> (same
    /// precedent as <see cref="DictationSettings.AudioDevice"/>), so this value
    /// never travels through the cloud-synced settings.json path. Task
    /// infrastructure-n3p8w.
    /// </summary>
    [JsonPropertyName("keepModelLoaded")]
    public bool KeepModelLoaded { get; set; }
}

public sealed class DictationSettings
{
    /// <summary>Preferred audio input device name (null = system default).</summary>
    [JsonPropertyName("audioDevice")]
    public string? AudioDevice { get; set; }

    /// <summary>Whisper language code (e.g. "en", "de"). Null = auto-detect.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// Text processing mode for dictation output. <c>Clean</c> (default) runs
    /// the deterministic filler-removal and whitespace-normalization pipeline
    /// before typing the text into the focused window. <c>Raw</c> ships the
    /// ASR output verbatim (useful for debugging and verbatim use cases).
    /// </summary>
    [JsonPropertyName("textMode")]
    public DictationTextMode TextMode { get; set; } = DictationTextMode.Clean;
}

/// <summary>
/// Selects whether dictation output is cleaned by the text-processing pipeline
/// before being typed, or shipped as the raw ASR output.
/// </summary>
public enum DictationTextMode
{
    /// <summary>Run the deterministic clean-text pipeline (filler removal, whitespace).</summary>
    Clean = 0,

    /// <summary>Ship raw ASR output untouched.</summary>
    Raw = 1,
}

public sealed class TemplateSettings
{
    /// <summary>User-defined text templates as name/text pairs.</summary>
    [JsonPropertyName("items")]
    public List<TemplateItem> Items { get; set; } = [];

    /// <summary>User-defined template groups for organizing templates.</summary>
    [JsonPropertyName("groups")]
    public List<TemplateGroup> Groups { get; set; } = [];
}

/// <summary>
/// A named text template that can be triggered by voice command.
/// </summary>
public sealed class TemplateItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The group this template belongs to. Null or empty means "Ungrouped".
    /// </summary>
    [JsonPropertyName("group")]
    public string? Group { get; set; }
}

/// <summary>
/// A named group for organizing templates.
/// </summary>
public sealed class TemplateGroup
{
    /// <summary>Display name of the group.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the group is expanded in the UI.</summary>
    [JsonPropertyName("isExpanded")]
    public bool IsExpanded { get; set; } = true;

    /// <summary>Display order (lower = higher in list).</summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }
}

/// <summary>
/// Settings for the dictation overlay indicator window.
/// The pill-shaped overlay appears at the last globally-clicked mouse position.
/// </summary>
public sealed class OverlaySettings
{
    /// <summary>Whether the overlay indicator is enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Overlay opacity (0.0 to 1.0).</summary>
    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 0.85;
}

/// <summary>
/// Persisted window size, position, and state.
/// </summary>
public sealed class WindowSettings
{
    /// <summary>Window left position (null = not yet saved).</summary>
    [JsonPropertyName("left")]
    public double? Left { get; set; }

    /// <summary>Window top position (null = not yet saved).</summary>
    [JsonPropertyName("top")]
    public double? Top { get; set; }

    /// <summary>Window width (null = use default).</summary>
    [JsonPropertyName("width")]
    public double? Width { get; set; }

    /// <summary>Window height (null = use default).</summary>
    [JsonPropertyName("height")]
    public double? Height { get; set; }

    /// <summary>Whether the window was maximized.</summary>
    [JsonPropertyName("isMaximized")]
    public bool IsMaximized { get; set; }

    /// <summary>Whether the sidebar was collapsed to icons-only mode.</summary>
    [JsonPropertyName("sidebarCollapsed")]
    public bool SidebarCollapsed { get; set; }
}

/// <summary>
/// Settings for Ollama-based local LLM transcript analysis.
/// The machine-local endpoint and model live in <see cref="BootstrapConfig"/>;
/// only the synced fields (analysis prompt templates) are stored here.
/// </summary>
public sealed class OllamaSettings
{
    /// <summary>
    /// Ollama API endpoint URL. Mirrored from <see cref="BootstrapConfig.OllamaEndpoint"/>
    /// by <see cref="WhisperHeim.Services.Settings.SettingsService"/> so existing
    /// read-sites on <c>AppSettings</c> keep working; writes go back to bootstrap.
    /// </summary>
    [JsonIgnore]
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Selected model name (e.g. "qwen2.5:14b"). Mirrored from
    /// <see cref="BootstrapConfig.OllamaModel"/> by
    /// <see cref="WhisperHeim.Services.Settings.SettingsService"/>; machine-local.
    /// </summary>
    [JsonIgnore]
    public string? Model { get; set; }

    /// <summary>User-defined and built-in analysis prompt templates.</summary>
    [JsonPropertyName("analysisTemplates")]
    public List<AnalysisPromptTemplate> AnalysisTemplates { get; set; } = [];
}
