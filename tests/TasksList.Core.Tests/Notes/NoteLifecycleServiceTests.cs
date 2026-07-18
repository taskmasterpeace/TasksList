using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.Core.Tests.Notes;

public sealed class NoteLifecycleServiceTests
{
    private static readonly DateTimeOffset Friday =
        new(2026, 7, 17, 16, 30, 0, TimeSpan.FromHours(-4));

    [Theory]
    [InlineData(SleepPreset.FifteenMinutes, 15)]
    [InlineData(SleepPreset.OneHour, 60)]
    public void RelativeSleepPresetsHideUntilTheCalculatedWakeTime(
        SleepPreset preset,
        int expectedMinutes)
    {
        var sleeping = NoteLifecycleService.ScheduleSleep(
            NotePresentation.Default(NoteId.New()),
            preset,
            Friday);

        Assert.Equal(Friday, sleeping.HiddenAt);
        Assert.Equal(Friday.AddMinutes(expectedMinutes), sleeping.WakeAt);
        Assert.False(NoteLifecycleService.Evaluate(sleeping, Friday).ShouldShow);
        Assert.True(NoteLifecycleService.Evaluate(
            sleeping,
            Friday.AddMinutes(expectedMinutes)).ShouldWake);
    }

    [Fact]
    public void TomorrowAndNextWorkdayUseEightAmInTheCurrentOffset()
    {
        var note = NotePresentation.Default(NoteId.New());

        var tomorrow = NoteLifecycleService.ScheduleSleep(note, SleepPreset.TomorrowMorning, Friday);
        var workday = NoteLifecycleService.ScheduleSleep(note, SleepPreset.NextWorkday, Friday);

        Assert.Equal(new DateTimeOffset(2026, 7, 18, 8, 0, 0, Friday.Offset), tomorrow.WakeAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 8, 0, 0, Friday.Offset), workday.WakeAt);
    }

    [Fact]
    public void DueReminderDescribesSoundPulseAndTopmostAttention()
    {
        var state = NotePresentation.Default(NoteId.New()) with
        {
            ReminderAt = Friday,
            ReminderAttention = ReminderAttention.SoundAndPulse,
        };

        var decision = NoteLifecycleService.Evaluate(state, Friday.AddSeconds(1));

        Assert.True(decision.ReminderDue);
        Assert.True(decision.PlaySound);
        Assert.True(decision.Pulse);
        Assert.True(decision.RequireTopmost);
    }

    [Fact]
    public void WakeAndAcknowledgeClearOnlyTheirOwnLifecycleState()
    {
        var state = NotePresentation.Default(NoteId.New()) with
        {
            HiddenAt = Friday,
            WakeAt = Friday,
            ReminderAt = Friday,
        };

        var awake = NoteLifecycleService.Wake(state, Friday.AddMinutes(1));
        var acknowledged = NoteLifecycleService.AcknowledgeReminder(awake, Friday.AddMinutes(2));

        Assert.Null(acknowledged.HiddenAt);
        Assert.Null(acknowledged.WakeAt);
        Assert.Null(acknowledged.ReminderAt);
    }

    [Fact]
    public void TrashBecomesPermanentlyDeletableAfterThirtyDays()
    {
        var deleted = NotePresentation.Default(NoteId.New()).SoftDelete(Friday);

        Assert.False(NoteLifecycleService.Evaluate(deleted, Friday.AddDays(29.99)).TrashExpired);
        Assert.True(NoteLifecycleService.Evaluate(deleted, Friday.AddDays(30)).TrashExpired);
        Assert.False(NoteLifecycleService.Evaluate(deleted, Friday.AddDays(30)).ShouldShow);
    }

    [Fact]
    public void CustomSleepRequiresAFutureDate()
    {
        var note = NotePresentation.Default(NoteId.New());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NoteLifecycleService.ScheduleSleep(note, SleepPreset.Custom, Friday, Friday));
    }
}
