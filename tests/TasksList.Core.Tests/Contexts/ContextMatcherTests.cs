using TasksList.Core.Contexts;

namespace TasksList.Core.Tests.Contexts;

public sealed class ContextMatcherTests
{
    [Fact]
    public void ExactWindowIdentityMatchesDespiteTitleCaseChanges()
    {
        var rule = new ContextMatchRule(
            "C:\\Program Files\\Docker\\Docker Desktop.exe",
            "HwndWrapper[Docker]",
            "Docker Desktop",
            null);
        var observed = new ObservedWindow(
            "c:\\program files\\docker\\docker desktop.exe",
            "HwndWrapper[Docker]",
            "DOCKER DESKTOP",
            null);

        Assert.True(ContextMatcher.IsMatch(rule, observed));
    }

    [Fact]
    public void ProjectHintAllowsAChangedTerminalTitleToMatch()
    {
        var rule = new ContextMatchRule(
            "C:\\Program Files\\WindowsApps\\wt.exe",
            "CASCADIA_HOSTING_WINDOW_CLASS",
            "Claude Code",
            "D:\\git\\taskslist");
        var observed = new ObservedWindow(
            "C:\\Program Files\\WindowsApps\\wt.exe",
            "CASCADIA_HOSTING_WINDOW_CLASS",
            "PowerShell — tests running",
            "D:\\git\\taskslist");

        Assert.True(ContextMatcher.IsMatch(rule, observed));
    }
}
