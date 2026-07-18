namespace TasksList.Core.Notes;

public enum SleepPreset
{
    FifteenMinutes,
    OneHour,
    TomorrowMorning,
    NextWorkday,
    Custom,
}

public sealed record NoteLifecycleDecision(
    bool ShouldShow,
    bool ShouldWake,
    bool ReminderDue,
    bool PlaySound,
    bool Pulse,
    bool RequireTopmost,
    bool TrashExpired);

public static class NoteLifecycleService
{
    public static NotePresentation ScheduleSleep(
        NotePresentation presentation,
        SleepPreset preset,
        DateTimeOffset now,
        DateTimeOffset? customWakeAt = null)
    {
        var wakeAt = preset switch
        {
            SleepPreset.FifteenMinutes => now.AddMinutes(15),
            SleepPreset.OneHour => now.AddHours(1),
            SleepPreset.TomorrowMorning => AtEightAm(now.AddDays(1)),
            SleepPreset.NextWorkday => NextWorkday(now),
            SleepPreset.Custom when customWakeAt is not null => customWakeAt.Value,
            SleepPreset.Custom => throw new ArgumentNullException(nameof(customWakeAt)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset)),
        };

        if (wakeAt <= now)
        {
            throw new ArgumentOutOfRangeException(
                nameof(customWakeAt),
                "A sleeping note must wake in the future.");
        }

        return presentation with
        {
            HiddenAt = now,
            WakeAt = wakeAt,
            ModifiedAt = now,
        };
    }

    public static NotePresentation ScheduleReminder(
        NotePresentation presentation,
        DateTimeOffset reminderAt,
        ReminderAttention attention,
        DateTimeOffset now,
        bool remainTopmost = true)
    {
        if (reminderAt <= now)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reminderAt),
                "A reminder must be scheduled in the future.");
        }

        return presentation with
        {
            ReminderAt = reminderAt,
            ReminderAttention = attention,
            ReminderTopmost = remainTopmost,
            ModifiedAt = now,
        };
    }

    public static NoteLifecycleDecision Evaluate(
        NotePresentation presentation,
        DateTimeOffset now)
    {
        var shouldWake = presentation.DeletedAt is null &&
                         presentation.WakeAt is not null &&
                         presentation.WakeAt <= now;
        var reminderDue = presentation.DeletedAt is null &&
                          presentation.ReminderAt is not null &&
                          presentation.ReminderAt <= now;
        var attention = presentation.ReminderAttention;
        var sound = reminderDue && attention is ReminderAttention.Sound or ReminderAttention.SoundAndPulse;
        var pulse = reminderDue && attention is ReminderAttention.Pulse or ReminderAttention.SoundAndPulse;
        var shouldShow = presentation.DeletedAt is null &&
                         (presentation.HiddenAt is null || shouldWake) &&
                         (presentation.WakeAt is null || shouldWake);

        return new NoteLifecycleDecision(
            shouldShow,
            shouldWake,
            reminderDue,
            sound,
            pulse,
            reminderDue && presentation.ReminderTopmost,
            presentation.DeletedAt is not null && presentation.DeletedAt <= now.AddDays(-30));
    }

    public static NotePresentation Wake(NotePresentation presentation, DateTimeOffset now) =>
        presentation with
        {
            HiddenAt = null,
            WakeAt = null,
            ModifiedAt = now,
        };

    public static NotePresentation AcknowledgeReminder(
        NotePresentation presentation,
        DateTimeOffset now) => presentation with
    {
        ReminderAt = null,
        ModifiedAt = now,
    };

    private static DateTimeOffset AtEightAm(DateTimeOffset date) =>
        new(date.Year, date.Month, date.Day, 8, 0, 0, date.Offset);

    private static DateTimeOffset NextWorkday(DateTimeOffset now)
    {
        var candidate = now.AddDays(1);
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            candidate = candidate.AddDays(1);
        }
        return AtEightAm(candidate);
    }
}
