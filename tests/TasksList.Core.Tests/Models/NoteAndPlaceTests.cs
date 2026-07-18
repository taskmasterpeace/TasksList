using TasksList.Core.Models;

namespace TasksList.Core.Tests.Models;

public sealed class NoteAndPlaceTests
{
    [Fact]
    public void AttachmentModeCanChangeAndAttachmentCanBeRemoved()
    {
        var contextId = ContextId.New();
        var attached = Note.Create("Title", "Body")
            .AttachTo(contextId, AttachmentVisibility.ForegroundOnly);

        var changed = attached.SetAttachmentVisibility(
            contextId,
            AttachmentVisibility.SleepUntilReturn);
        var detached = changed.DetachFrom(contextId);

        Assert.Equal(AttachmentVisibility.SleepUntilReturn, changed.Attachments.Single().Visibility);
        Assert.Empty(detached.Attachments);
    }

    [Fact]
    public void NoteCanAttachToAContextWithoutEmbeddingContextMetadataInMarkdown()
    {
        var note = Note.Create("Docker later", "# Docker cleanup\n\n- [ ] Prune images");
        var context = ContextRef.Create(
            ContextKind.Application,
            "windows",
            "docker-desktop",
            "Docker Desktop");

        var attached = note.AttachTo(context.Id, AttachmentVisibility.ForegroundOnly);

        Assert.Equal(note.Markdown, attached.Markdown);
        Assert.Single(attached.Attachments);
        Assert.Equal(context.Id, attached.Attachments[0].ContextId);
    }

    [Fact]
    public void PlaceCarriesAStableParentAndSavedTabOrdering()
    {
        var browser = Place.Create(PlaceKind.Browser, "Microsoft Edge", null, "edge");
        var session = Place.Create(PlaceKind.BrowserSession, "Release research", browser.Id, "session:release");
        var tab = SavedTab.Create(session.Id, "https://example.com/docs", "Docs", 0, 2);

        Assert.Equal(browser.Id, session.ParentId);
        Assert.Equal(session.Id, tab.SessionPlaceId);
        Assert.Equal(2, tab.TabIndex);
    }

    [Fact]
    public void UpdatingNoteContentPreservesIdentityAndAttachments()
    {
        var contextId = ContextId.New();
        var note = Note.Create("Draft", "# First")
            .AttachTo(contextId, AttachmentVisibility.WhilePresent);

        var updated = note.UpdateContent("Docker checklist", "# Docker\n\n- [ ] Build");

        Assert.Equal(note.Id, updated.Id);
        Assert.Equal("Docker checklist", updated.Title);
        Assert.Equal("# Docker\n\n- [ ] Build", updated.Markdown);
        Assert.Single(updated.Attachments);
    }
}
