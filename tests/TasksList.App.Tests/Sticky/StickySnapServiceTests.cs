using TasksList.App.Sticky;

namespace TasksList.App.Tests.Sticky;

public sealed class StickySnapServiceTests
{
    private static readonly WindowBounds WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void NoteSnapsToEveryNearbyWorkAreaEdge()
    {
        Assert.Equal(0, Snap(new WindowBounds(7, 100, 300, 200)).Left);
        Assert.Equal(0, Snap(new WindowBounds(100, 8, 300, 200)).Top);
        Assert.Equal(1620, Snap(new WindowBounds(1611, 100, 300, 200)).Left);
        Assert.Equal(840, Snap(new WindowBounds(100, 831, 300, 200)).Top);
    }

    [Fact]
    public void NoteSnapsBesideAnotherSticky()
    {
        var anchor = new WindowBounds(100, 120, 300, 220);
        var moving = new WindowBounds(408, 125, 280, 200);

        var snapped = StickySnapService.Snap(moving, [anchor], WorkArea, 12, bypass: false);

        Assert.Equal(400, snapped.Left);
        Assert.Equal(120, snapped.Top);
    }

    [Fact]
    public void BeyondToleranceDoesNotSnap()
    {
        var requested = new WindowBounds(20, 30, 300, 200);

        Assert.Equal(requested, StickySnapService.Snap(requested, [], WorkArea, 12, false));
    }

    [Fact]
    public void AltBypassReturnsRequestedBoundsUnchanged()
    {
        var requested = new WindowBounds(4, 5, 300, 200);

        Assert.Equal(requested, StickySnapService.Snap(requested, [], WorkArea, 12, true));
    }

    private static WindowBounds Snap(WindowBounds requested) =>
        StickySnapService.Snap(requested, [], WorkArea, 12, bypass: false);
}
