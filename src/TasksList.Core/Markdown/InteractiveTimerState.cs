using TasksList.Core.Models;

namespace TasksList.Core.Markdown;

public sealed record InteractiveTimerState(
    NoteId NoteId,
    int BlockIndex,
    int DurationSeconds,
    int RemainingSeconds,
    bool IsRunning,
    DateTimeOffset? EndsAt,
    DateTimeOffset ModifiedAt)
{
    public static InteractiveTimerState Default(
        NoteId noteId,
        int blockIndex,
        int minutes,
        DateTimeOffset now) => new(
        noteId,
        blockIndex,
        minutes * 60,
        minutes * 60,
        false,
        null,
        now);

    public int RemainingAt(DateTimeOffset now) => IsRunning && EndsAt is { } end
        ? Math.Max(0, (int)Math.Ceiling((end - now).TotalSeconds))
        : RemainingSeconds;
}
