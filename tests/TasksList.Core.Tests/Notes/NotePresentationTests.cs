using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.Core.Tests.Notes;

public sealed class NotePresentationTests
{
    [Fact]
    public void ArchiveHidesWithoutDeletingAndRestoreReturnsItToLibrary()
    {
        var noteId = NoteId.New();
        var archivedAt = DateTimeOffset.Parse("2026-07-18T23:00:00-04:00");
        var presentation = NotePresentation.Default(noteId, archivedAt.AddHours(-1));

        var archived = presentation.Archive(archivedAt);

        Assert.Equal(archivedAt, archived.HiddenAt);
        Assert.Null(archived.DeletedAt);
        Assert.Null(archived.Restore(archivedAt.AddMinutes(1)).HiddenAt);
    }
}
