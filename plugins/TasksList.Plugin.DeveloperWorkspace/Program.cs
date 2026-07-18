using System.Text.Json;
using TasksList.Plugin.DeveloperWorkspace;

if (args.Contains("--manifest", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(JsonSerializer.Serialize(DeveloperWorkspacePlugin.Manifest));
    return;
}

Console.WriteLine("Developer Workspace is ready for Task'sList handoff requests.");

