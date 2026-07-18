namespace TasksList.App.Sticky;

public readonly record struct WindowBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public readonly record struct RelativePlacement(
    double HorizontalRatio,
    double VerticalRatio,
    double Width,
    double Height);

public static class StickyWindowPlacement
{
    public static WindowBounds ClampToMonitors(
        WindowBounds requested,
        IReadOnlyList<WindowBounds> monitors)
    {
        if (monitors.Count == 0)
        {
            return requested;
        }

        var monitor = ClosestMonitor(requested, monitors);
        var width = Math.Min(requested.Width, monitor.Width);
        var height = Math.Min(requested.Height, monitor.Height);
        var left = Math.Clamp(requested.Left, monitor.Left, monitor.Right - width);
        var top = Math.Clamp(requested.Top, monitor.Top, monitor.Bottom - height);
        return new WindowBounds(left, top, width, height);
    }

    public static WindowBounds ClosestMonitor(
        WindowBounds requested,
        IReadOnlyList<WindowBounds> monitors) =>
        monitors.Count == 0
            ? requested
            : monitors.OrderBy(candidate => DistanceSquared(requested, candidate)).First();

    public static WindowBounds RestoreRelative(
        WindowBounds applicationWindow,
        RelativePlacement placement) =>
        new(
            applicationWindow.Left + (applicationWindow.Width * placement.HorizontalRatio),
            applicationWindow.Top + (applicationWindow.Height * placement.VerticalRatio),
            placement.Width,
            placement.Height);

    private static double DistanceSquared(WindowBounds note, WindowBounds monitor)
    {
        var noteCenterX = note.Left + (note.Width / 2);
        var noteCenterY = note.Top + (note.Height / 2);
        var monitorCenterX = monitor.Left + (monitor.Width / 2);
        var monitorCenterY = monitor.Top + (monitor.Height / 2);
        var deltaX = noteCenterX - monitorCenterX;
        var deltaY = noteCenterY - monitorCenterY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}
