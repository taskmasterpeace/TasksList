using TasksList.Core.Models;

namespace TasksList.Core.Clipboard;

public sealed record ClipboardQuery
{
    public string? Text { get; init; }
    public bool? Favorite { get; init; }
    public bool? Used { get; init; }
    public bool? Unfiled { get; init; }
    public HashSet<ContextId> SourceContextIds { get; init; } = [];
    public HashSet<CaptureKind> Kinds { get; init; } = [];
    public DateTimeOffset? CapturedAfter { get; init; }
    public DateTimeOffset? CapturedBefore { get; init; }
    public bool IncludeDeleted { get; init; }

    public bool Matches(Capture capture)
    {
        if (!IncludeDeleted && capture.DeletedAt is not null) return false;
        if (Favorite is not null && capture.IsFavorite != Favorite) return false;
        if (Used is not null && (capture.UsedAt is not null) != Used) return false;
        if (Unfiled is not null &&
            (capture.Assignments.Count == 0 && capture.AssignedNoteIds.Count == 0) != Unfiled) return false;
        if (SourceContextIds.Count > 0 && !SourceContextIds.Contains(capture.SourceContextId)) return false;
        if (Kinds.Count > 0 && !Kinds.Contains(capture.Kind)) return false;
        if (CapturedAfter is not null && capture.CapturedAt < CapturedAfter) return false;
        if (CapturedBefore is not null && capture.CapturedAt > CapturedBefore) return false;
        if (!string.IsNullOrWhiteSpace(Text))
        {
            var text = Text.Trim();
            if (!capture.Title.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !capture.PreviewText.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !capture.TextRepresentations.Values.Any(value =>
                    value.Contains(text, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }
        return true;
    }
}
