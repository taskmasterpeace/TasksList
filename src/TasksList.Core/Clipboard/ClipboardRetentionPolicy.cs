using TasksList.Core.Models;

namespace TasksList.Core.Clipboard;

public sealed record ClipboardRetentionOptions(TimeSpan? MaximumAge, long? MaximumBytes);

public sealed record RetainedCapture(
    CaptureId Id,
    DateTimeOffset CapturedAt,
    long Size,
    bool IsFavorite);

public static class ClipboardRetentionPolicy
{
    public static IReadOnlyList<CaptureId> ChooseExpired(
        IReadOnlyCollection<RetainedCapture> captures,
        ClipboardRetentionOptions options,
        DateTimeOffset now)
    {
        var deleted = new HashSet<CaptureId>();
        if (options.MaximumAge is { } maximumAge)
        {
            foreach (var capture in captures.Where(capture =>
                         !capture.IsFavorite && now - capture.CapturedAt > maximumAge))
            {
                deleted.Add(capture.Id);
            }
        }

        if (options.MaximumBytes is { } maximumBytes)
        {
            var retainedBytes = captures
                .Where(capture => !deleted.Contains(capture.Id))
                .Sum(capture => capture.Size);
            foreach (var capture in captures
                         .Where(capture => !capture.IsFavorite && !deleted.Contains(capture.Id))
                         .OrderBy(capture => capture.CapturedAt))
            {
                if (retainedBytes <= maximumBytes)
                {
                    break;
                }

                deleted.Add(capture.Id);
                retainedBytes -= capture.Size;
            }
        }

        return deleted.ToArray();
    }
}
