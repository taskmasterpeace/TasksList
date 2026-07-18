using System.Collections.Immutable;

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
        string previewText,
        DateTimeOffset capturedAt,
        ImmutableArray<Assignment> assignments,
        ImmutableDictionary<string, string> textRepresentations)
    {
        Id = id;
        Kind = kind;
        SourceContextId = sourceContextId;
        PreviewText = previewText;
        CapturedAt = capturedAt;
        Assignments = assignments;
        TextRepresentations = textRepresentations;
    }

    public CaptureId Id { get; }

    public CaptureKind Kind { get; }

    public ContextId SourceContextId { get; }

    public string PreviewText { get; }

    public DateTimeOffset CapturedAt { get; }

    public IReadOnlyList<Assignment> Assignments { get; }

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
            previewText,
            capturedAt,
            ImmutableArray<Assignment>.Empty,
            ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase));

    public static Capture Restore(
        CaptureId id,
        CaptureKind kind,
        ContextId sourceContextId,
        string previewText,
        DateTimeOffset capturedAt,
        IEnumerable<Assignment> assignments,
        IEnumerable<KeyValuePair<string, string>>? textRepresentations = null) =>
        new(
            id,
            kind,
            sourceContextId,
            previewText,
            capturedAt,
            assignments.ToImmutableArray(),
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

        return new Capture(
            Id,
            Kind,
            SourceContextId,
            PreviewText,
            CapturedAt,
            Assignments.Append(assignment).ToImmutableArray(),
            TextRepresentations.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));
    }

    public Capture WithTextRepresentation(string mediaType, string content) =>
        new(
            Id,
            Kind,
            SourceContextId,
            PreviewText,
            CapturedAt,
            Assignments.ToImmutableArray(),
            TextRepresentations
                .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase)
                .SetItem(mediaType, content));
}
