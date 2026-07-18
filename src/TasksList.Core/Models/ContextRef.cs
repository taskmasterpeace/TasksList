namespace TasksList.Core.Models;

public enum ContextKind
{
    Computer,
    Application,
    Window,
    Browser,
    BrowserWindow,
    BrowserTab,
    Conversation,
    Project,
    Folder,
    File,
}

public sealed record ContextRef(
    ContextId Id,
    ContextKind Kind,
    string Provider,
    string StableIdentity,
    string DisplayName)
{
    public static ContextRef Create(
        ContextKind kind,
        string provider,
        string stableIdentity,
        string displayName) =>
        new(ContextId.New(), kind, provider, stableIdentity, displayName);
}

