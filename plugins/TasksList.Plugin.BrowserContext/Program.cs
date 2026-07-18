using System.Text.Json;
using TasksList.Plugin.BrowserContext;

if (args.Contains("--manifest", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(JsonSerializer.Serialize(BrowserContextPlugin.Manifest));
    return;
}

var bridgePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TasksList",
    "bridge",
    "browser-tabs.json");
await NativeMessagingHost.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), bridgePath);
