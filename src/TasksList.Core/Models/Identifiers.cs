namespace TasksList.Core.Models;

public readonly record struct NoteId(Guid Value)
{
    public static NoteId New() => new(Guid.NewGuid());
}

public readonly record struct CaptureId(Guid Value)
{
    public static CaptureId New() => new(Guid.NewGuid());
}

public readonly record struct ContextId(Guid Value)
{
    public static ContextId New() => new(Guid.NewGuid());
}

public readonly record struct PlaceId(Guid Value)
{
    public static PlaceId New() => new(Guid.NewGuid());
}

public readonly record struct AssignmentId(Guid Value)
{
    public static AssignmentId New() => new(Guid.NewGuid());
}

public readonly record struct AttachmentId(Guid Value)
{
    public static AttachmentId New() => new(Guid.NewGuid());
}

public readonly record struct SavedTabId(Guid Value)
{
    public static SavedTabId New() => new(Guid.NewGuid());
}

public readonly record struct ActivityId(Guid Value)
{
    public static ActivityId New() => new(Guid.NewGuid());
}

