namespace TasksList.Core.Models;

public enum AssignmentActor
{
    User,
    Plugin,
    Agent,
}

public sealed record Assignment(
    AssignmentId Id,
    PlaceId PlaceId,
    AssignmentActor Actor,
    DateTimeOffset FiledAt);

