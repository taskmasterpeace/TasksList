using System.IO;
using TasksList.Core.Models;

namespace TasksList.App.Clipboard;

public sealed record ClipboardCaptureCandidate(
    ContextRef Source,
    HashSet<string> Formats,
    long SizeBytes,
    bool IsSelfPaste);

public sealed record ClipboardCaptureRules(
    bool Paused,
    List<string> ExcludedApplications,
    long MaximumBytes);

public static class ClipboardCapturePolicy
{
    private static readonly string[] PrivateFormats =
    [
        "Clipboard Viewer Ignore",
        "ExcludeClipboardContentFromMonitorProcessing",
    ];

    public static bool ShouldCapture(
        ClipboardCaptureCandidate candidate,
        ClipboardCaptureRules rules)
    {
        if (rules.Paused || candidate.IsSelfPaste || candidate.SizeBytes > rules.MaximumBytes)
        {
            return false;
        }
        if (candidate.Formats.Any(format => PrivateFormats.Contains(
                format,
                StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        var separator = candidate.Source.StableIdentity.IndexOf('|');
        var path = separator >= 0
            ? candidate.Source.StableIdentity[..separator]
            : candidate.Source.StableIdentity;
        var executable = Path.GetFileName(path);
        return !rules.ExcludedApplications.Any(excluded =>
            string.Equals(excluded, executable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(excluded, path, StringComparison.OrdinalIgnoreCase));
    }
}
