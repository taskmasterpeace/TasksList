using TasksList.Core.Models;
using TasksList.Core.Places;

namespace TasksList.Core.Tests.Places;

public sealed class PlaceServiceTests
{
    [Fact]
    public void ManualGroupsCanBeNestedUnderDetectedApplications()
    {
        var browser = Place.Create(PlaceKind.Browser, "Microsoft Edge", null, "edge");
        var service = new PlaceService([browser]);

        var research = service.AddManualGroup(browser.Id, "Research");
        var readLater = service.AddManualGroup(research.Id, "Read later");

        Assert.Equal(browser.Id, research.ParentId);
        Assert.Equal(research.Id, readLater.ParentId);
        Assert.Equal([research.Id], service.ChildrenOf(browser.Id).Select(place => place.Id));
    }

    [Fact]
    public void MovingAPlaceUnderItsDescendantIsRejected()
    {
        var root = Place.Create(PlaceKind.ManualGroup, "Root", null, "manual:root");
        var service = new PlaceService([root]);
        var child = service.AddManualGroup(root.Id, "Child");

        var exception = Assert.Throws<InvalidOperationException>(() => service.Move(root.Id, child.Id));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

