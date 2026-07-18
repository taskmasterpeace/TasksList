using System.Text.Json;
using TasksList.Plugin.CaptureWorkflows;

if (args.Contains("--manifest", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(JsonSerializer.Serialize(CaptureWorkflowsPlugin.Manifest));
    return;
}

Console.WriteLine("Capture Workflows is ready for Task'sList pipeline requests.");

