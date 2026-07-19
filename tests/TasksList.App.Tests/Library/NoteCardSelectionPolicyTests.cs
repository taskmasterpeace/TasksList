using TasksList.App.Library;

namespace TasksList.App.Tests.Library;

public sealed class NoteCardSelectionPolicyTests
{
    [Fact]
    public void RightClickOnUnselectedCardSelectsOnlyThatCard() =>
        Assert.Equal([3], NoteCardSelectionPolicy.ResolveRightClick(3, [1, 2]));

    [Fact]
    public void RightClickOnSelectedCardPreservesMultiSelection() =>
        Assert.Equal([1, 3, 5], NoteCardSelectionPolicy.ResolveRightClick(3, [1, 3, 5]));
}
