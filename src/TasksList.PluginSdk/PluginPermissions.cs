namespace TasksList.PluginSdk;

public sealed class PluginPermissionException : InvalidOperationException
{
    public PluginPermissionException(string message) : base(message)
    {
    }
}

public sealed class PluginPermissionSet
{
    private readonly HashSet<PluginCapability> _grants;

    public PluginPermissionSet(string pluginId, IEnumerable<PluginCapability> grants)
    {
        PluginId = pluginId;
        _grants = grants.ToHashSet();
    }

    public string PluginId { get; }

    public bool IsGranted(PluginCapability capability) => _grants.Contains(capability);

    public void Demand(PluginCapability capability)
    {
        if (!IsGranted(capability))
        {
            throw new PluginPermissionException(
                $"Plugin '{PluginId}' has not been granted '{capability.ToManifestName()}'.");
        }
    }
}

public abstract record PluginOperation(PluginCapability RequiredCapability);

public sealed record CreateNoteOperation(string Title, string Markdown, string? ContextIdentity)
    : PluginOperation(PluginCapability.NotesWrite);

public sealed record CreatePlaceOperation(string Name, string Kind, string? ParentIdentity)
    : PluginOperation(PluginCapability.PlacesWrite);

public static class PluginOperationValidator
{
    public static void Validate(PluginOperation operation, PluginPermissionSet permissions)
    {
        permissions.Demand(operation.RequiredCapability);
        if (operation is CreateNoteOperation note &&
            (string.IsNullOrWhiteSpace(note.Title) || string.IsNullOrWhiteSpace(note.Markdown)))
        {
            throw new InvalidDataException("A plugin-created note must have a title and Markdown body.");
        }
        if (operation is CreatePlaceOperation place && string.IsNullOrWhiteSpace(place.Name))
        {
            throw new InvalidDataException("A plugin-created Place must have a name.");
        }
    }
}
