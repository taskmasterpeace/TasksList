using TasksList.Core.Models;
using TasksList.Core.Notes;
using TasksList.Infrastructure.Storage;

namespace TasksList.App.Library;

public interface INoteCardCommandStore
{
    Task SaveNoteAsync(Note note);
    Task<NotePresentation> GetPresentationAsync(NoteId noteId);
    Task SavePresentationAsync(NotePresentation presentation);
}

public sealed class TasksListNoteCardCommandStore(TasksListDatabase database)
    : INoteCardCommandStore
{
    public Task SaveNoteAsync(Note note) => database.SaveNoteAsync(note);

    public Task<NotePresentation> GetPresentationAsync(NoteId noteId) =>
        database.GetNotePresentationAsync(noteId);

    public Task SavePresentationAsync(NotePresentation presentation) =>
        database.SaveNotePresentationAsync(presentation);
}

public sealed class NoteCardCommandService(
    INoteCardCommandStore store,
    Action<Note> open,
    Action<string> copyMarkdown,
    Action<NoteId> closeOpenSticky)
{
    public void Open(IReadOnlyList<Note> notes)
    {
        foreach (var note in notes) open(note);
    }

    public async Task<IReadOnlyList<Note>> DuplicateAsync(
        IReadOnlyList<Note> notes,
        DateTimeOffset now)
    {
        var duplicates = new List<Note>(notes.Count);
        foreach (var source in notes)
        {
            var duplicate = Note.Create($"{source.Title} copy", source.Markdown);
            foreach (var attachment in source.Attachments)
            {
                duplicate = duplicate.AttachTo(attachment.ContextId, attachment.Visibility);
            }

            await store.SaveNoteAsync(duplicate);
            var sourcePresentation = await store.GetPresentationAsync(source.Id);
            var bounds = sourcePresentation.Bounds;
            await store.SavePresentationAsync(sourcePresentation with
            {
                NoteId = duplicate.Id,
                Bounds = bounds with { Left = bounds.Left + 24, Top = bounds.Top + 24 },
                HiddenAt = null,
                DeletedAt = null,
                WakeAt = null,
                CreatedAt = now,
                ModifiedAt = now,
            });
            duplicates.Add(duplicate);
        }

        return duplicates;
    }

    public void CopyMarkdown(IReadOnlyList<Note> notes) =>
        copyMarkdown(string.Join(
            $"{Environment.NewLine}---{Environment.NewLine}",
            notes.Select(note => note.Markdown)));

    public async Task ArchiveAsync(IReadOnlyList<Note> notes, DateTimeOffset now)
    {
        foreach (var note in notes)
        {
            var presentation = await store.GetPresentationAsync(note.Id);
            await store.SavePresentationAsync(presentation.Archive(now));
            closeOpenSticky(note.Id);
        }
    }

    public async Task MoveToTrashAsync(IReadOnlyList<Note> notes, DateTimeOffset now)
    {
        foreach (var note in notes)
        {
            var presentation = await store.GetPresentationAsync(note.Id);
            await store.SavePresentationAsync(presentation.SoftDelete(now));
            closeOpenSticky(note.Id);
        }
    }

    public async Task<IReadOnlyList<Note>> AttachAsync(
        IReadOnlyList<Note> notes,
        ContextRef context)
    {
        var updated = new List<Note>(notes.Count);
        foreach (var note in notes)
        {
            var attached = note.AttachTo(context.Id, AttachmentVisibility.WhilePresent);
            await store.SaveNoteAsync(attached);
            updated.Add(attached);
        }

        return updated;
    }
}
