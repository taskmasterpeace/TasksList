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
        ImmutableArray<Assignment> assignments)
    {
        Id = id;
        Kind = kind;
        SourceContextId = sourceContextId;
        PreviewText = previewText;
        CapturedAt = capturedAt;
        Assignments = assignments;
    }

    public CaptureId Id { get; }

    public CaptureKind Kind { get; }

    public ContextId SourceContextId { get; }

    public string PreviewText { get; }

    public DateTimeOffset CapturedAt { get; }

    public IReadOnlyList<Assignment> Assignments { get; }

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
            ImmutableArray<Assignment>.Empty);

    public static Capture Restore(
        CaptureId id,
        CaptureKind kind,
        ContextId sourceContextId,
        string previewText,
        DateTimeOffset capturedAt,
        IEnumerable<Assignment> assignments) =>
        new(id, kind, sourceContextId, previewText, capturedAt, assignments.ToImmutableArray());

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
            Assignments.Append(assignment).ToImmutableArray());
    }
}
