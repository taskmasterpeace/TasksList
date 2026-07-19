using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using TasksList.Core.Models;

namespace TasksList.App.Shell;

public sealed record WindowsNotificationRequest(
    string Title,
    string Message,
    IReadOnlyDictionary<string, string> Arguments,
    string ActionLabel);

public sealed class WindowsNotificationActivation(NoteId noteId) : EventArgs
{
    public NoteId NoteId { get; } = noteId;
}

public static partial class WindowsNotificationPolicy
{
    public const int MaximumPreviewLength = 160;

    public static WindowsNotificationRequest ForReminder(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var preview = MarkdownSyntax().Replace(note.Markdown, " ");
        preview = Whitespace().Replace(preview, " ").Trim();
        if (string.IsNullOrWhiteSpace(preview)) preview = "This note is due now.";
        if (preview.Length > MaximumPreviewLength)
        {
            preview = $"{preview[..(MaximumPreviewLength - 1)].TrimEnd()}…";
        }

        return new WindowsNotificationRequest(
            $"Reminder: {note.Title}",
            preview,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["action"] = "open-note",
                ["noteId"] = note.Id.Value.ToString("D"),
            }),
            "Open note");
    }

    public static WindowsNotificationActivation? ParseActivation(
        IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.TryGetValue("action", out var action) && action == "open-note" &&
               arguments.TryGetValue("noteId", out var noteIdText) &&
               Guid.TryParse(noteIdText, out var noteId)
            ? new WindowsNotificationActivation(new NoteId(noteId))
            : null;
    }

    [GeneratedRegex(@"(?m)^\s{0,3}(?:#{1,6}\s*|[-*+]\s+(?:\[[ xX]\]\s*)?|>\s*)|[`*_~]+|!?\[[^\]]*\]\([^)]*\)")]
    private static partial Regex MarkdownSyntax();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
