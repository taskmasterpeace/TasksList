namespace TasksList.App.Sticky;

public static class StickySnapService
{
    public static WindowBounds Snap(
        WindowBounds moving,
        IReadOnlyList<WindowBounds> otherNotes,
        WindowBounds workArea,
        double tolerance,
        bool bypass)
    {
        if (bypass || tolerance < 0)
        {
            return moving;
        }

        var xCandidates = new List<double>
        {
            workArea.Left,
            workArea.Right - moving.Width,
        };
        var yCandidates = new List<double>
        {
            workArea.Top,
            workArea.Bottom - moving.Height,
        };

        foreach (var note in otherNotes)
        {
            xCandidates.Add(note.Left);
            xCandidates.Add(note.Right);
            xCandidates.Add(note.Left - moving.Width);
            xCandidates.Add(note.Right - moving.Width);
            yCandidates.Add(note.Top);
            yCandidates.Add(note.Bottom);
            yCandidates.Add(note.Top - moving.Height);
            yCandidates.Add(note.Bottom - moving.Height);
        }

        var left = NearestWithin(moving.Left, xCandidates, tolerance);
        var top = NearestWithin(moving.Top, yCandidates, tolerance);
        return new WindowBounds(left, top, moving.Width, moving.Height);
    }

    private static double NearestWithin(
        double requested,
        IEnumerable<double> candidates,
        double tolerance)
    {
        var nearest = candidates
            .Select(candidate => (Value: candidate, Distance: Math.Abs(candidate - requested)))
            .OrderBy(candidate => candidate.Distance)
            .First();
        return nearest.Distance <= tolerance ? nearest.Value : requested;
    }
}
