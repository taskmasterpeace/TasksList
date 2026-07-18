using TasksList.App.Sticky;

namespace TasksList.App.Tests.Sticky;

public sealed class StickyWindowPlacementTests
{
    [Fact]
    public void ClosestMonitorUsesTheDisplayContainingTheSticky()
    {
        var primary = new WindowBounds(0, 0, 1920, 1040);
        var left = new WindowBounds(-1600, 0, 1600, 900);
        var sticky = new WindowBounds(-900, 200, 320, 240);

        Assert.Equal(left, StickyWindowPlacement.ClosestMonitor(sticky, [primary, left]));
    }

    [Fact]
    public void ClampMovesAnOffscreenNoteOntoTheNearestMonitor()
    {
        var note = new WindowBounds(4200, 200, 360, 280);
        var monitors = new[]
        {
            new WindowBounds(0, 0, 1920, 1080),
            new WindowBounds(1920, 0, 1920, 1080),
        };

        var result = StickyWindowPlacement.ClampToMonitors(note, monitors);

        Assert.Equal(3480, result.Left);
        Assert.Equal(200, result.Top);
    }

    [Fact]
    public void RestoreRelativeKeepsNoteBesideItsApplicationWindow()
    {
        var application = new WindowBounds(100, 100, 1200, 800);
        var relative = new RelativePlacement(0.75, 0.10, 320, 260);

        var result = StickyWindowPlacement.RestoreRelative(application, relative);

        Assert.Equal(1000, result.Left);
        Assert.Equal(180, result.Top);
        Assert.Equal(320, result.Width);
        Assert.Equal(260, result.Height);
    }
}
