using System.Collections.Immutable;

namespace TasksList.Core.Models;

public enum AttachmentVisibility
{
    WhilePresent,
    ForegroundOnly,
    RemainVisible,
    SleepUntilReturn,
}

public sealed record Attachment(
    AttachmentId Id,
    ContextId ContextId,
    AttachmentVisibility Visibility);

public sealed record Note
{
    private Note(
        NoteId id,
        string title,
        string markdown,
        ImmutableArray<Attachment> attachments)
    {
        Id = id;
        Title = title;
        Markdown = markdown;
        Attachments = attachments;
    }

    public NoteId Id { get; }

    public string Title { get; }

    public string Markdown { get; }

    public IReadOnlyList<Attachment> Attachments { get; }

    public static Note Create(string title, string markdown) =>
        new(NoteId.New(), title, markdown, ImmutableArray<Attachment>.Empty);

    public static Note Restore(
        NoteId id,
        string title,
        string markdown,
        IEnumerable<Attachment> attachments) =>
        new(id, title, markdown, attachments.ToImmutableArray());

    public Note AttachTo(ContextId contextId, AttachmentVisibility visibility)
    {
        if (Attachments.Any(attachment => attachment.ContextId == contextId))
        {
            return this;
        }

        var attachment = new Attachment(AttachmentId.New(), contextId, visibility);
        return new Note(Id, Title, Markdown, Attachments.Append(attachment).ToImmutableArray());
    }

    public Note UpdateContent(string title, string markdown) =>
        new(Id, title, markdown, Attachments.ToImmutableArray());
}
