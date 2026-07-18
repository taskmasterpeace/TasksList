using TasksList.Core.Models;

namespace TasksList.Core.Tests.Models;

public sealed class NoteAndPlaceTests
{
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
}
