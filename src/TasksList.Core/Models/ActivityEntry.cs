namespace TasksList.Core.Models;

public enum ActivityActorKind
{
    User,
    Host,
    Plugin,
    Agent,
}

public sealed record ActivityEntry(
    ActivityId Id,
    ActivityActorKind ActorKind,
    string ActorName,
    string Operation,
    string TargetId,
    string Reason,
    DateTimeOffset OccurredAt);

