namespace TasksList.App.Library;

public static class NoteCardSelectionPolicy
{
    public static IReadOnlyList<int> ResolveRightClick(
        int clickedIndex,
        IReadOnlyCollection<int> selectedIndices)
    {
        ArgumentNullException.ThrowIfNull(selectedIndices);
        if (clickedIndex < 0) return selectedIndices.Order().ToArray();

        return selectedIndices.Contains(clickedIndex)
            ? selectedIndices.Order().ToArray()
            : [clickedIndex];
    }
}
