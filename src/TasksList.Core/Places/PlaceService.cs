using TasksList.Core.Models;

namespace TasksList.Core.Places;

public sealed class PlaceService
{
    private readonly Dictionary<PlaceId, Place> _places;

    public PlaceService(IEnumerable<Place> places) =>
        _places = places.ToDictionary(place => place.Id);

    public Place AddManualGroup(PlaceId? parentId, string name)
    {
        if (parentId is { } parent && !_places.ContainsKey(parent))
        {
            throw new KeyNotFoundException("The parent Place does not exist.");
        }

        var place = Place.Create(
            PlaceKind.ManualGroup,
            name.Trim(),
            parentId,
            $"manual:{Guid.NewGuid():N}");
        _places.Add(place.Id, place);
        return place;
    }

    public IReadOnlyList<Place> ChildrenOf(PlaceId? parentId) =>
        _places.Values
            .Where(place => place.ParentId == parentId)
            .OrderBy(place => place.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public Place Move(PlaceId placeId, PlaceId? newParentId)
    {
        if (!_places.TryGetValue(placeId, out var place))
        {
            throw new KeyNotFoundException("The Place to move does not exist.");
        }

        var cursor = newParentId;
        while (cursor is { } candidate)
        {
            if (candidate == placeId)
            {
                throw new InvalidOperationException("Moving this Place would create a cycle.");
            }

            cursor = _places.TryGetValue(candidate, out var parent)
                ? parent.ParentId
                : throw new KeyNotFoundException("The destination Place does not exist.");
        }

        var moved = place with { ParentId = newParentId };
        _places[placeId] = moved;
        return moved;
    }
}

