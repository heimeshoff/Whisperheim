using System.Windows;
using System.Windows.Input;
using WhisperHeim.Services.Templates;

namespace WhisperHeim.Views;

/// <summary>
/// Centered modal shown when a template-mode dictation matched no template
/// (task main-t9w2k). Turns the dead-end "no match" toast into an invitation to
/// create the missing template inline: the trigger term is pre-filled with the
/// transcribed (possibly misheard) word and is editable, and a multiline body is
/// entered for the replacement text.
///
/// Creation is <b>save-only</b> — it persists via <see cref="ITemplateService.AddTemplate"/>
/// and does not type the body into the previously-focused app. All validation and
/// persistence rules live in <see cref="InlineTemplateCreationModel"/> so they are
/// unit-tested without a UI.
/// </summary>
public partial class InlineTemplateDialog : Window
{
    private readonly InlineTemplateCreationModel _model;

    public InlineTemplateDialog(ITemplateService templateService, string rawText)
    {
        InitializeComponent();

        _model = new InlineTemplateCreationModel(templateService, rawText);

        PromptText.Text =
            $"No template matched “{rawText}”. Create one now — " +
            "fix the trigger term if it was misheard, then enter the text it should expand to.";
        TermTextBox.Text = _model.Term;

        Loaded += (_, _) =>
        {
            TermTextBox.Focus();
            TermTextBox.SelectAll();
        };
    }

    /// <summary>True if a template was created and persisted.</summary>
    public bool Created { get; private set; }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Create_Click(object sender, RoutedEventArgs e) => TryCreate();

    private void TermTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Enter in the single-line term field moves to the body rather than
        // submitting, so an empty body cannot be saved by accident.
        if (e.Key == Key.Enter)
        {
            BodyTextBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void BodyTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        // The body is multiline (AcceptsReturn), so plain Enter inserts a newline.
        // Ctrl+Enter submits; Escape cancels.
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            TryCreate();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void TryCreate()
    {
        _model.Term = TermTextBox.Text;
        _model.Body = BodyTextBox.Text;

        if (!_model.TryCreate())
        {
            // Term or body empty — keep the dialog open and focus the missing field.
            if (string.IsNullOrWhiteSpace(_model.Term))
                TermTextBox.Focus();
            else
                BodyTextBox.Focus();
            return;
        }

        Created = true;
        Close();
    }
}
