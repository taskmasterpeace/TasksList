using System.IO.Compression;
using TasksList.App.Plugins;

namespace TasksList.App.Tests.Plugins;

public sealed class PluginPackageInstallerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"taskslist-plugin-install-{Guid.NewGuid():N}");

    public PluginPackageInstallerTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void ValidPackageInstallsIntoVersionedPluginDirectory()
    {
        var package = Path.Combine(_directory, "valid.taskplugin");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "plugin.json", """
                { "id": "sample", "name": "Sample", "version": "1.2.3", "apiVersion": 1, "entryPoint": "Sample.exe", "capabilities": ["notes.write"] }
                """);
            WriteEntry(archive, "Sample.exe", "not-an-executable-for-this-contract-test");
        }

        var installed = PluginPackageInstaller.Install(package, Path.Combine(_directory, "plugins"));

        Assert.Equal("sample", installed.Manifest.Id);
        Assert.True(File.Exists(Path.Combine(installed.DirectoryPath, "Sample.exe")));
        Assert.EndsWith(Path.Combine("sample", "1.2.3"), installed.DirectoryPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackagePathTraversalIsRejectedBeforeExtraction()
    {
        var package = Path.Combine(_directory, "bad.taskplugin");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "plugin.json", """
                { "id": "bad", "name": "Bad", "version": "1.0.0", "apiVersion": 1, "entryPoint": "Bad.exe", "capabilities": [] }
                """);
            WriteEntry(archive, "../outside.txt", "bad");
        }

        Assert.Throws<InvalidDataException>(() =>
            PluginPackageInstaller.Install(package, Path.Combine(_directory, "plugins")));
        Assert.False(File.Exists(Path.Combine(_directory, "outside.txt")));
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
