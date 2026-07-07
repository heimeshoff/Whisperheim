namespace WhisperHeim.Services.Templates;

/// <summary>
/// Validation + persistence logic for the inline "no template matched, create one
/// now" flow (task main-t9w2k). Factored out of the WPF dialog so the rules can be
/// unit-tested without a UI:
///
/// <list type="bullet">
/// <item>The <see cref="Term"/> is pre-filled with the transcribed (possibly
/// misheard) spoken text and is editable, so the trigger word can be corrected
/// before saving.</item>
/// <item>Creation requires a non-empty term and a non-empty body, mirroring the
/// start-page drawer's rule (empty term → no nameless template; empty body
/// rejected the same way the drawer rejects it).</item>
/// <item>Creation is <b>save-only</b>: it persists the template via
/// <see cref="ITemplateService.AddTemplate"/> only. It never types the body into
/// the previously-focused app — this model has no input-simulation dependency by
/// design, which is what makes "save-only" structurally guaranteed.</item>
/// </list>
/// </summary>
public sealed class InlineTemplateCreationModel
{
    private readonly ITemplateService _templateService;

    public InlineTemplateCreationModel(ITemplateService templateService, string rawText)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        Term = rawText ?? string.Empty;
        Body = string.Empty;
    }

    /// <summary>
    /// The trigger word for the template (→ <c>TemplateItem.Name</c>). Pre-filled
    /// with the transcribed spoken text; editable so a mishearing can be corrected.
    /// </summary>
    public string Term { get; set; }

    /// <summary>
    /// The replacement text the template expands to (→ <c>TemplateItem.Text</c>).
    /// Empty by default.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// True when the current <see cref="Term"/> and <see cref="Body"/> would produce
    /// a valid template (both non-empty after trimming).
    /// </summary>
    public bool CanCreate =>
        !string.IsNullOrWhiteSpace(Term) && !string.IsNullOrWhiteSpace(Body);

    /// <summary>
    /// Persists a new ungrouped user template from the current term and body via the
    /// same path the start-page drawer uses. Returns <c>false</c> (and persists
    /// nothing) when the term or body is empty.
    /// </summary>
    public bool TryCreate()
    {
        if (!CanCreate)
            return false;

        _templateService.AddTemplate(Term.Trim(), Body.Trim(), group: null);
        return true;
    }
}
