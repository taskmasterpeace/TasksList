using TasksList.App.Library;
using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.App.Tests.Library;

public sealed class NoteCardCommandServiceTests
{
    private sealed class FakeStore : INoteCardCommandStore
    {
        public FakeStore(Note note, NotePresentation presentation)
        {
            Notes[note.Id] = note;
            Presentations[note.Id] = presentation;
        }

        public Dictionary<NoteId, Note> Notes { get; } = [];
        public Dictionary<NoteId, NotePresentation> Presentations { get; } = [];

        public Task SaveNoteAsync(Note note)
        {
            Notes[note.Id] = note;
            return Task.CompletedTask;
        }

        public Task<NotePresentation> GetPresentationAsync(NoteId noteId) =>
            Task.FromResult(Presentations[noteId]);

        public Task SavePresentationAsync(NotePresentation presentation)
        {
            Presentations[presentation.NoteId] = presentation;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DuplicateCopiesContentAndPresentationWithoutLifecycleFlags()
    {
        var source = Note.Create("Plan", "# Plan");
        var store = new FakeStore(source, NotePresentation.Default(source.Id) with
        {
            Topmost = false,
            HiddenAt = DateTimeOffset.UtcNow,
            DeletedAt = DateTimeOffset.UtcNow,
        });
        var service = new NoteCardCommandService(store, _ => { }, _ => { }, _ => { });

        var duplicates = await service.DuplicateAsync([source], DateTimeOffset.UtcNow);

        var duplicate = Assert.Single(duplicates);
        Assert.Equal("Plan copy", duplicate.Title);
        Assert.Equal(source.Markdown, duplicate.Markdown);
        Assert.Null(store.Presentations[duplicate.Id].HiddenAt);
        Assert.Null(store.Presentations[duplicate.Id].DeletedAt);
    }

    [Fact]
    public async Task TrashAndArchiveUseDistinctRecoverableStates()
    {
        var note = Note.Create("Plan", "# Plan");
        var store = new FakeStore(note, NotePresentation.Default(note.Id));
        var closed = new List<NoteId>();
        var service = new NoteCardCommandService(store, _ => { }, _ => { }, closed.Add);
        var now = DateTimeOffset.UtcNow;

        await service.ArchiveAsync([note], now);
        Assert.Equal(now, store.Presentations[note.Id].HiddenAt);
        Assert.Null(store.Presentations[note.Id].DeletedAt);

        await service.MoveToTrashAsync([note], now.AddMinutes(1));
        Assert.Equal(now.AddMinutes(1), store.Presentations[note.Id].DeletedAt);
        Assert.Contains(note.Id, closed);
    }
}
