namespace TasksList.App.Tests.Shell;

public sealed class WindowsCursorContractTests
{
    [Fact]
    public void CommandsAndCaptionsUseTheStandardWindowsPointer()
    {
        var appDirectory = Path.Combine(FindRepositoryRoot(), "src", "TasksList.App");
        var violations = Directory.EnumerateFiles(appDirectory, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = index + 1, Text = line }))
            .Where(item =>
                item.Text.Contains("Cursor=\"Hand\"", StringComparison.Ordinal) ||
                item.Text.Contains("Property=\"Cursor\" Value=\"Hand\"", StringComparison.Ordinal) ||
                item.Text.Contains("Cursor=\"SizeAll\"", StringComparison.Ordinal))
            .Select(item => $"{Path.GetRelativePath(appDirectory, item.Path)}:{item.Line}")
            .ToArray();

        Assert.Empty(violations);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
