using Microsoft.Data.Sqlite;
using TasksList.Core.Models;
using TasksList.Core.Notes;
using TasksList.Core.Markdown;
using TasksList.Infrastructure.Storage;

namespace TasksList.Infrastructure.Tests.Storage;

public sealed class NotePresentationDatabaseTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"taskslist-presentation-tests-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_directory, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task EveryPresentationFieldRoundTrips()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "taskslist.db"));
        await database.InitializeAsync();
        var note = Note.Create("Styled", "# Durable");
        await database.SaveNoteAsync(note);
        var now = DateTimeOffset.Parse("2026-07-18T15:30:00-04:00");
        var presentation = NotePresentation.Default(note.Id) with
        {
            Bounds = new NoteBounds(101, 202, 503, 404, 390, "DISPLAY2"),
            Preset = PaperPreset.Graphite,
            BackgroundHex = "#202126",
            TextHex = "#F7F3EA",
            AccentHex = "#F0B95A",
            ActiveOpacity = 0.65,
            InactiveOpacity = 0.42,
            FontFamily = "Cascadia Mono",
            FontSize = 17,
            FontWeight = 600,
            LineSpacing = 1.4,
            Density = NoteDensity.Spacious,
            ToolbarVisibility = ToolbarVisibility.Focused,
            ShadowStrength = 0.7,
            CornerStyle = CornerStyle.Round,
            BorderVisible = false,
            TextureEnabled = false,
            Topmost = false,
            Rolled = true,
            Locked = true,
            Ghost = true,
            HiddenAt = now,
            DeletedAt = now.AddMinutes(1),
            WakeAt = now.AddHours(1),
            ReminderAt = now.AddHours(2),
            ReminderAttention = ReminderAttention.SoundAndPulse,
            CreatedAt = now.AddDays(-1),
            ModifiedAt = now,
        };

        await database.SaveNotePresentationAsync(presentation);
        var loaded = await database.GetNotePresentationAsync(note.Id);

        Assert.Equal(presentation, loaded);
    }

    [Fact]
    public async Task InteractiveTimerRuntimeStateRoundTripsSeparatelyFromMarkdown()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "timer.db"));
        await database.InitializeAsync();
        var note = Note.Create("Focus", ":::timer minutes=25 label=\"Focus\"");
        await database.SaveNoteAsync(note);
        var now = DateTimeOffset.Parse("2026-07-18T20:00:00Z");
        var state = new InteractiveTimerState(
            note.Id,
            0,
            1500,
            1200,
            true,
            now.AddMinutes(20),
            now);

        await database.SaveInteractiveTimerStateAsync(state);
        var loaded = await database.GetInteractiveTimerStateAsync(note.Id, 0);

        Assert.Equal(state, loaded);
    }

    [Fact]
    public async Task ExistingVersionOneDatabaseReceivesSafeDefaults()
    {
        var databasePath = Path.Combine(_directory, "legacy.db");
        await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE notes (id TEXT PRIMARY KEY, title TEXT NOT NULL, markdown TEXT NOT NULL);";
            await command.ExecuteNonQueryAsync();
        }

        var database = new TasksListDatabase(databasePath);
        await database.InitializeAsync();
        var note = Note.Create("Legacy", "still here");
        await database.SaveNoteAsync(note);

        var presentation = await database.GetNotePresentationAsync(note.Id);
        var loadedNote = await database.GetNoteAsync(note.Id);

        Assert.Equal(NotePresentation.Default(note.Id), presentation);
        Assert.Equal("still here", loadedNote?.Markdown);
    }

    [Fact]
    public async Task NamedStylesRoundTripInNameOrderAndPreserveDefaultFlag()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "styles.db"));
        await database.InitializeAsync();
        var later = new NamedNoteStyle(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "Work",
            NoteStyle.FromPreset(PaperPreset.Sky),
            false,
            DateTimeOffset.Parse("2026-07-18T15:00:00-04:00"));
        var first = new NamedNoteStyle(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "Graphite",
            NoteStyle.FromPreset(PaperPreset.Graphite) with { ActiveOpacity = 0.65 },
            true,
            DateTimeOffset.Parse("2026-07-18T15:01:00-04:00"));

        await database.SaveNamedStyleAsync(later);
        await database.SaveNamedStyleAsync(first);
        var loaded = await database.ListNamedStylesAsync();

        Assert.Equal(new[] { "Graphite", "Work" }, loaded.Select(style => style.Name));
        Assert.True(loaded[0].IsDefault);
        Assert.Equal(0.65, loaded[0].Style.ActiveOpacity);
    }
}
