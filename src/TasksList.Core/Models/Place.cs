namespace TasksList.Core.Models;

public enum PlaceKind
{
    Computer,
    Application,
    Browser,
    BrowserWindow,
    BrowserSession,
    BrowserTab,
    Conversation,
    Project,
    Folder,
    File,
    ManualGroup,
}

public sealed record Place(
    PlaceId Id,
    PlaceKind Kind,
    string Name,
    PlaceId? ParentId,
    string StableIdentity)
{
    public static Place Create(
        PlaceKind kind,
        string name,
        PlaceId? parentId,
        string stableIdentity) =>
        new(PlaceId.New(), kind, name, parentId, stableIdentity);
}

public sealed record SavedTab(
    SavedTabId Id,
    PlaceId SessionPlaceId,
    string Url,
    string Title,
    int WindowIndex,
    int TabIndex)
{
    public static SavedTab Create(
        PlaceId sessionPlaceId,
        string url,
        string title,
        int windowIndex,
        int tabIndex) =>
        new(SavedTabId.New(), sessionPlaceId, url, title, windowIndex, tabIndex);
}

