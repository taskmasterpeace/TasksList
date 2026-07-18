using TasksList.PluginSdk;

namespace TasksList.PluginSdk.Tests;

public sealed class PluginPermissionTests
{
    [Fact]
    public void DeniedCapabilityCannotBeUsedEvenWhenManifestRequestsIt()
    {
        var grants = new PluginPermissionSet(
            "taskslist.browser-context",
            [PluginCapability.BrowserTabsIdentity]);

        var exception = Assert.Throws<PluginPermissionException>(() =>
            grants.Demand(PluginCapability.BrowserPageContent));

        Assert.Contains("browser.page.content", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyGrantedTypedOperationsReachTheHost()
    {
        var grants = new PluginPermissionSet(
            "taskslist.capture-workflows",
            [PluginCapability.NotesWrite]);
        var operation = new CreateNoteOperation("Bug evidence", "# Evidence", null);

        PluginOperationValidator.Validate(operation, grants);

        Assert.Equal("Bug evidence", operation.Title);
    }
}
