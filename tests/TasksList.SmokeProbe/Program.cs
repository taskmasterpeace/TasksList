using TasksList.Core.Notes;
using TasksList.Infrastructure.Storage;

if (args.Length == 4 && string.Equals(args[0], "lifecycle", StringComparison.OrdinalIgnoreCase))
{
    var database = new TasksListDatabase(args[1]);
    await database.InitializeAsync();
    var note = (await database.ListNotesAsync()).FirstOrDefault(item =>
        string.Equals(item.Title, args[2], StringComparison.Ordinal));
    if (note is null)
    {
        Console.Error.WriteLine($"Note '{args[2]}' was not found.");
        return 5;
    }

    var now = DateTimeOffset.Now;
    var presentation = await database.GetNotePresentationAsync(note.Id);
    presentation = args[3].ToLowerInvariant() switch
    {
        "wake" => presentation with
        {
            HiddenAt = now.AddSeconds(-1),
            WakeAt = now.AddSeconds(-1),
            ModifiedAt = now,
        },
        "sleep" => presentation with
        {
            HiddenAt = now,
            WakeAt = now.AddMinutes(15),
            ModifiedAt = now,
        },
        "reminder" => presentation with
        {
            ReminderAt = now.AddSeconds(-1),
            ReminderAttention = ReminderAttention.SoundAndPulse,
            ReminderTopmost = true,
            ModifiedAt = now,
        },
        _ => throw new ArgumentException("Lifecycle action must be sleep, wake, or reminder."),
    };
    await database.SaveNotePresentationAsync(presentation);
    Console.WriteLine($"Forced lifecycle action {args[3]} for {note.Title}.");
    return 0;
}

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: TasksList.SmokeProbe <database-path> <expected-capture-text>");
    Console.Error.WriteLine("   or: TasksList.SmokeProbe lifecycle <database-path> <note-title> <sleep|wake|reminder>");
    return 2;
}

var captureDatabase = new TasksListDatabase(args[0]);
await captureDatabase.InitializeAsync();
var captures = await captureDatabase.SearchCapturesAsync(args[1], 20);
var capture = captures.FirstOrDefault(item => string.Equals(item.PreviewText, args[1], StringComparison.Ordinal));
if (capture is null)
{
    Console.Error.WriteLine("Expected clipboard capture was not stored.");
    return 3;
}

var source = await captureDatabase.GetContextAsync(capture.SourceContextId);
if (source is null || string.IsNullOrWhiteSpace(source.DisplayName))
{
    Console.Error.WriteLine("Clipboard capture did not retain source context.");
    return 4;
}

Console.WriteLine($"Captured: {capture.PreviewText} | Source: {source.DisplayName}");
return 0;
