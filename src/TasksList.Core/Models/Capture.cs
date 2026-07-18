using System.Collections.Immutable;
using System.Text;

namespace TasksList.Core.Models;

public enum CaptureKind
{
    Text,
    Html,
    RichText,
    Markdown,
    Image,
    Files,
}

public sealed record Capture
{
    private Capture(
        CaptureId id,
        CaptureKind kind,
        ContextId sourceContextId,
        string title,
        string previewText,
        DateTimeOffset capturedAt,
        bool isFavorite,
        DateTimeOffset? usedAt,
        DateTimeOffset? deletedAt,
        DateTimeOffset modifiedAt,
        long sizeBytes,
        string? sourceUrl,
        string duplicateHash,
        ImmutableArray<Assignment> assignments,
        ImmutableHashSet<NoteId> assignedNoteIds,
        ImmutableDictionary<string, string> textRepresentations)
    {
        Id = id;
        Kind = kind;
        SourceContextId = sourceContextId;
        Title = title;
        PreviewText = previewText;
        CapturedAt = capturedAt;
        IsFavorite = isFavorite;
        UsedAt = usedAt;
        DeletedAt = deletedAt;
        ModifiedAt = modifiedAt;
        SizeBytes = sizeBytes;
        SourceUrl = sourceUrl;
        DuplicateHash = duplicateHash;
        Assignments = assignments;
        AssignedNoteIds = assignedNoteIds;
        TextRepresentations = textRepresentations;
    }

    public CaptureId Id { get; }
    public CaptureKind Kind { get; }
    public ContextId SourceContextId { get; }
    public string Title { get; }
    public string PreviewText { get; }
    public DateTimeOffset CapturedAt { get; }
    public bool IsFavorite { get; }
    public DateTimeOffset? UsedAt { get; }
    public DateTimeOffset? DeletedAt { get; }
    public DateTimeOffset ModifiedAt { get; }
    public long SizeBytes { get; }
    public string? SourceUrl { get; }
    public string DuplicateHash { get; }
    public IReadOnlyList<Assignment> Assignments { get; }
    public IReadOnlySet<NoteId> AssignedNoteIds { get; }
    public IReadOnlyDictionary<string, string> TextRepresentations { get; }

    public static Capture Create(
        CaptureKind kind,
        ContextId sourceContextId,
        string previewText,
        DateTimeOffset capturedAt) =>
        new(
            CaptureId.New(),
            kind,
            sourceContextId,
            string.Empty,
            previewText,
            capturedAt,
            false,
            null,
            null,
            capturedAt,
            Encoding.UTF8.GetByteCount(previewText),
            null,
            string.Empty,
            ImmutableArray<Assignment>.Empty,
            ImmutableHashSet<NoteId>.Empty,
            ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase));

    public static Capture Restore(
        CaptureId id,
        CaptureKind kind,
        ContextId sourceContextId,
        string previewText,
        DateTimeOffset capturedAt,
        IEnumerable<Assignment> assignments,
        IEnumerable<KeyValuePair<string, string>>? textRepresentations = null,
        string title = "",
        bool isFavorite = false,
        DateTimeOffset? usedAt = null,
        DateTimeOffset? deletedAt = null,
        DateTimeOffset? modifiedAt = null,
        long? sizeBytes = null,
        string? sourceUrl = null,
        string duplicateHash = "",
        IEnumerable<NoteId>? assignedNoteIds = null) =>
        new(
            id,
            kind,
            sourceContextId,
            title,
            previewText,
            capturedAt,
            isFavorite,
            usedAt,
            deletedAt,
            modifiedAt ?? capturedAt,
            sizeBytes ?? Encoding.UTF8.GetByteCount(previewText),
            sourceUrl,
            duplicateHash,
            assignments.ToImmutableArray(),
            (assignedNoteIds ?? []).ToImmutableHashSet(),
            (textRepresentations ?? [])
                .ToImmutableDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase));

    public Capture AssignTo(PlaceId placeId, AssignmentActor actor)
    {
        if (Assignments.Any(assignment => assignment.PlaceId == placeId))
        {
            return this;
        }

        var assignment = new Assignment(
            AssignmentId.New(),
            placeId,
            actor,
            DateTimeOffset.UtcNow);
        return Copy(assignments: Assignments.Append(assignment).ToImmutableArray());
    }

    public Capture WithTextRepresentation(string mediaType, string content)
    {
        var representations = TextRepresentations
            .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase)
            .SetItem(mediaType, content);
        return Copy(
            textRepresentations: representations,
            sizeBytes: representations.Sum(item => Encoding.UTF8.GetByteCount(item.Value)));
    }

    public Capture AssignToNote(NoteId noteId) =>
        Copy(assignedNoteIds: AssignedNoteIds.ToImmutableHashSet().Add(noteId));

    public Capture WithFavorite(bool favorite) => Copy(isFavorite: favorite);
    public Capture MarkUsed(DateTimeOffset usedAt) =>
        Copy(usedAt: usedAt, replaceUsedAt: true, modifiedAt: usedAt);
    public Capture Rename(string title, DateTimeOffset? modifiedAt = null) =>
        Copy(title: title.Trim(), modifiedAt: modifiedAt ?? DateTimeOffset.UtcNow);
    public Capture Edit(string previewText, DateTimeOffset? modifiedAt = null) => Copy(
        previewText: previewText,
        modifiedAt: modifiedAt ?? DateTimeOffset.UtcNow,
        sizeBytes: Encoding.UTF8.GetByteCount(previewText));
    public Capture ReplaceWithPlainText(string text, DateTimeOffset modifiedAt) => new(
        Id,
        CaptureKind.Text,
        SourceContextId,
        Title,
        text,
        CapturedAt,
        IsFavorite,
        UsedAt,
        DeletedAt,
        modifiedAt,
        Encoding.UTF8.GetByteCount(text),
        SourceUrl,
        string.Empty,
        Assignments.ToImmutableArray(),
        AssignedNoteIds.ToImmutableHashSet(),
        ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
            .Add("text/plain", text));
    public Capture SoftDelete(DateTimeOffset deletedAt) =>
        Copy(deletedAt: deletedAt, replaceDeletedAt: true, modifiedAt: deletedAt);
    public Capture RestoreDeleted(DateTimeOffset restoredAt) =>
        Copy(deletedAt: null, replaceDeletedAt: true, modifiedAt: restoredAt);
    public Capture WithSourceUrl(string? sourceUrl) => Copy(sourceUrl: sourceUrl);
    public Capture WithDuplicateHash(string hash) => Copy(duplicateHash: hash);
    public Capture Promote(DateTimeOffset copiedAt) => Copy(
        capturedAt: copiedAt,
        modifiedAt: copiedAt,
        deletedAt: null,
        replaceDeletedAt: true);

    public Capture Duplicate(DateTimeOffset copiedAt) => new(
        CaptureId.New(),
        Kind,
        SourceContextId,
        Title.Length == 0 ? string.Empty : $"{Title} copy",
        PreviewText,
        copiedAt,
        false,
        null,
        null,
        copiedAt,
        SizeBytes,
        SourceUrl,
        DuplicateHash,
        Assignments.ToImmutableArray(),
        AssignedNoteIds.ToImmutableHashSet(),
        TextRepresentations.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));

    private Capture Copy(
        string? title = null,
        string? previewText = null,
        DateTimeOffset? capturedAt = null,
        bool? isFavorite = null,
        DateTimeOffset? usedAt = null,
        DateTimeOffset? deletedAt = null,
        bool replaceUsedAt = false,
        bool replaceDeletedAt = false,
        DateTimeOffset? modifiedAt = null,
        long? sizeBytes = null,
        string? sourceUrl = null,
        string? duplicateHash = null,
        ImmutableArray<Assignment>? assignments = null,
        ImmutableHashSet<NoteId>? assignedNoteIds = null,
        ImmutableDictionary<string, string>? textRepresentations = null) =>
        new(
            Id,
            Kind,
            SourceContextId,
            title ?? Title,
            previewText ?? PreviewText,
            capturedAt ?? CapturedAt,
            isFavorite ?? IsFavorite,
            replaceUsedAt ? usedAt : UsedAt,
            replaceDeletedAt ? deletedAt : DeletedAt,
            modifiedAt ?? ModifiedAt,
            sizeBytes ?? SizeBytes,
            sourceUrl ?? SourceUrl,
            duplicateHash ?? DuplicateHash,
            assignments ?? Assignments.ToImmutableArray(),
            assignedNoteIds ?? AssignedNoteIds.ToImmutableHashSet(),
            textRepresentations ?? TextRepresentations.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));
}
