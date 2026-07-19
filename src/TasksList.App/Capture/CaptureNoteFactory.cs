using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Capture;

public static class CaptureNoteFactory
{
    private const string PayloadPathRepresentation = "application/x-taskslist-payload-path";

    public static Note Create(CaptureModel capture, ContextRef source)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(source);

        var body = capture.Kind == CaptureKind.Image &&
                   capture.TextRepresentations.TryGetValue(PayloadPathRepresentation, out var payloadPath)
            ? $"![Captured region](<{payloadPath}>)\n\n**Size:** {CaptureDimensions(capture.PreviewText)}"
            : capture.TextRepresentations.TryGetValue("text/plain", out var plain)
                ? plain
                : capture.PreviewText;

        return Note.Create(
                $"Capture from {source.DisplayName}",
                $"# Capture from {source.DisplayName}\n\n{body}\n\n**Source:** {source.DisplayName}")
            .AttachTo(source.Id, AttachmentVisibility.WhilePresent);
    }

    private static string CaptureDimensions(string preview) =>
        preview.StartsWith("Screen capture · ", StringComparison.Ordinal)
            ? preview[17..]
            : preview.StartsWith("Screen capture - ", StringComparison.Ordinal)
                ? preview[17..]
                : preview;
}
