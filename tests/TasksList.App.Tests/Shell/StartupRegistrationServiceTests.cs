using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void BuildCommand_QuotesExecutablePath()
    {
        Assert.Equal(
            "\"C:\\Program Files\\Task'sList\\TasksList.App.exe\" --startup",
            StartupRegistrationService.BuildCommand("C:\\Program Files\\Task'sList\\TasksList.App.exe"));
    }
}
