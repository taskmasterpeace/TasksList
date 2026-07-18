using System.Text.RegularExpressions;
using TasksList.PluginSdk;

namespace TasksList.Plugin.CaptureWorkflows;

public sealed record SensitiveCaptureResult(
    bool IsSensitive,
    bool NetworkAllowed,
    DateTimeOffset? ExpiresAt);

public static partial class CaptureWorkflowsPlugin
{
    public static PluginManifest Manifest { get; } = new(
        "taskslist.capture-workflows",
        "Capture Workflows",
        "1.0.0",
        1,
        "TasksList.Plugin.CaptureWorkflows.exe",
        [PluginCapability.ClipboardRead, PluginCapability.ScreenCapture, PluginCapability.NotesWrite]);

    public static SensitiveCaptureResult ProtectSensitiveCapture(string content, DateTimeOffset now)
    {
        var sensitive = SecretRegex().IsMatch(content);
        return sensitive
            ? new SensitiveCaptureResult(true, false, now.AddMinutes(5))
            : new SensitiveCaptureResult(false, true, null);
    }

    [GeneratedRegex("(?i)(api[_-]?key|token|password|secret)\\s*[:=]\\s*[^\\s]{8,}|sk-[A-Za-z0-9_-]{12,}")]
    private static partial Regex SecretRegex();
}

