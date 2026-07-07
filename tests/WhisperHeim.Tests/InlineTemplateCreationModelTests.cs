using WhisperHeim.Models;
using WhisperHeim.Services.Templates;

namespace WhisperHeim.Tests;

public class InlineTemplateCreationModelTests
{
    /// <summary>
    /// Records calls to AddTemplate so tests can assert save-only behavior:
    /// exactly one persistence call, the right arguments, and nothing else.
    /// </summary>
    private sealed class RecordingTemplateService : ITemplateService
    {
        public List<(string Name, string Text, string? Group)> Added { get; } = new();

        public void AddTemplate(string name, string text, string? group = null)
            => Added.Add((name, text, group));

        // --- unused members ---
        public TemplateMatchResult? MatchAndExpand(string spokenText) => null;
        public IReadOnlyList<TemplateItem> GetTemplates() => Array.Empty<TemplateItem>();
        public void UpdateTemplate(int index, string name, string text) { }
        public void RemoveTemplate(int index) { }
        public void MoveTemplateToGroup(int templateIndex, string? groupName) { }
        public IReadOnlyList<TemplateGroup> GetGroups() => Array.Empty<TemplateGroup>();
        public void AddGroup(string name) { }
        public void RenameGroup(string oldName, string newName) { }
        public bool RemoveGroup(string name) => false;
        public void ReorderGroups(IReadOnlyList<string> groupNamesInOrder) { }
        public void SetGroupExpanded(string groupName, bool isExpanded) { }
        public void EnsureDefaults() { }
        public IReadOnlyList<SystemTemplate> GetSystemTemplates() => Array.Empty<SystemTemplate>();
    }

    [Fact]
    public void Term_IsPreFilledWithTranscribedText()
    {
        var model = new InlineTemplateCreationModel(new RecordingTemplateService(), "wibble");

        Assert.Equal("wibble", model.Term);
    }

    [Fact]
    public void Body_IsEmptyByDefault()
    {
        var model = new InlineTemplateCreationModel(new RecordingTemplateService(), "wibble");

        Assert.Equal(string.Empty, model.Body);
    }

    [Fact]
    public void CanCreate_IsFalse_WhenTermIsEmpty()
    {
        var model = new InlineTemplateCreationModel(new RecordingTemplateService(), "")
        {
            Body = "some body"
        };

        Assert.False(model.CanCreate);
    }

    [Fact]
    public void CanCreate_IsFalse_WhenBodyIsEmpty()
    {
        var model = new InlineTemplateCreationModel(new RecordingTemplateService(), "trigger");

        Assert.False(model.CanCreate);
    }

    [Fact]
    public void CanCreate_IsTrue_WhenTermAndBodyArePresent()
    {
        var model = new InlineTemplateCreationModel(new RecordingTemplateService(), "trigger")
        {
            Body = "some body"
        };

        Assert.True(model.CanCreate);
    }

    [Fact]
    public void TryCreate_WithEmptyTerm_DoesNotPersistAndReturnsFalse()
    {
        var service = new RecordingTemplateService();
        var model = new InlineTemplateCreationModel(service, "")
        {
            Body = "some body"
        };

        var created = model.TryCreate();

        Assert.False(created);
        Assert.Empty(service.Added);
    }

    [Fact]
    public void TryCreate_WithEmptyBody_DoesNotPersistAndReturnsFalse()
    {
        var service = new RecordingTemplateService();
        var model = new InlineTemplateCreationModel(service, "trigger");

        var created = model.TryCreate();

        Assert.False(created);
        Assert.Empty(service.Added);
    }

    [Fact]
    public void TryCreate_WithEditedTermAndBody_PersistsTrimmedUngroupedTemplate()
    {
        var service = new RecordingTemplateService();
        var model = new InlineTemplateCreationModel(service, "misherd")
        {
            // user corrects the misheard trigger word and types a body
            Term = "  greeting  ",
            Body = "  Hello there!  "
        };

        var created = model.TryCreate();

        Assert.True(created);
        var added = Assert.Single(service.Added);
        Assert.Equal("greeting", added.Name);
        Assert.Equal("Hello there!", added.Text);
        Assert.Null(added.Group); // ungrouped
    }

    [Fact]
    public void TryCreate_PersistsExactlyOnce_AndNothingElse_SaveOnly()
    {
        // The model depends only on ITemplateService.AddTemplate; there is no
        // input-simulation path, so "save-only" is observable as: a successful
        // create produces one AddTemplate call and no other side effect.
        var service = new RecordingTemplateService();
        var model = new InlineTemplateCreationModel(service, "trigger")
        {
            Body = "body"
        };

        model.TryCreate();

        Assert.Single(service.Added);
    }
}
