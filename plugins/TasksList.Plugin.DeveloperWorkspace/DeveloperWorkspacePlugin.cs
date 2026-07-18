using System.Text;
using TasksList.PluginSdk;

namespace TasksList.Plugin.DeveloperWorkspace;

public static class DeveloperWorkspacePlugin
{
    public static PluginManifest Manifest { get; } = new(
        "taskslist.developer-workspace",
        "Developer Workspace",
        "1.0.0",
        1,
        "TasksList.Plugin.DeveloperWorkspace.exe",
        [PluginCapability.ProcessRead, PluginCapability.FilesReadSelected, PluginCapability.NotesWrite]);

    public static CreateNoteOperation CreateHandoff(
        string repositoryPath,
        string branch,
        IReadOnlyCollection<string> changedFiles,
        string nextStep)
    {
        var markdown = new StringBuilder()
            .AppendLine($"# Where I left off · {Path.GetFileName(repositoryPath)}")
            .AppendLine()
            .AppendLine($"**Branch:** `{branch}`")
            .AppendLine()
            .AppendLine("## Changed files");
        foreach (var file in changedFiles)
        {
            markdown.AppendLine($"- `{file}`");
        }
        markdown
            .AppendLine()
            .AppendLine("## Next step")
            .AppendLine()
            .AppendLine($"- [ ] {nextStep}");
        return new CreateNoteOperation(
            $"Resume {Path.GetFileName(repositoryPath)}",
            markdown.ToString().TrimEnd(),
            $"repository:{repositoryPath}");
    }
}

