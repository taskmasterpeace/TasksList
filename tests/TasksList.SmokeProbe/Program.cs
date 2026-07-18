using TasksList.Infrastructure.Storage;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: TasksList.SmokeProbe <database-path> <expected-capture-text>");
    return 2;
}

var database = new TasksListDatabase(args[0]);
await database.InitializeAsync();
var captures = await database.SearchCapturesAsync(args[1], 20);
var capture = captures.FirstOrDefault(item => string.Equals(item.PreviewText, args[1], StringComparison.Ordinal));
if (capture is null)
{
    Console.Error.WriteLine("Expected clipboard capture was not stored.");
    return 3;
}

var source = await database.GetContextAsync(capture.SourceContextId);
if (source is null || string.IsNullOrWhiteSpace(source.DisplayName))
{
    Console.Error.WriteLine("Clipboard capture did not retain source context.");
    return 4;
}

Console.WriteLine($"Captured: {capture.PreviewText} | Source: {source.DisplayName}");
return 0;
