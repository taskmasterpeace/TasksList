using TasksList.Core.Contexts;
using TasksList.Core.Models;

namespace TasksList.Core.Tests.Contexts;

public sealed class AttachmentVisibilityPolicyTests
{
    [Fact]
    public void UnattachedNotesRemainVisibleEverywhere()
    {
        var note = Note.Create("Global", "Everywhere");

        Assert.True(AttachmentVisibilityPolicy.ShouldShow(note, ContextId.New()));
    }

    [Fact]
    public void ForegroundAttachmentShowsOnlyInItsMatchingContext()
    {
        var attachedContext = ContextId.New();
        var note = Note.Create("Docker", "Attached")
            .AttachTo(attachedContext, AttachmentVisibility.ForegroundOnly);

        Assert.True(AttachmentVisibilityPolicy.ShouldShow(note, attachedContext));
        Assert.False(AttachmentVisibilityPolicy.ShouldShow(note, ContextId.New()));
    }

    [Fact]
    public void RemainVisibleAttachmentDoesNotHideWhenContextChanges()
    {
        var note = Note.Create("Pinned", "Remain")
            .AttachTo(ContextId.New(), AttachmentVisibility.RemainVisible);

        Assert.True(AttachmentVisibilityPolicy.ShouldShow(note, ContextId.New()));
    }
}
