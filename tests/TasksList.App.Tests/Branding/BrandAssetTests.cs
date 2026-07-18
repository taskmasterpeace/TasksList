using System.Drawing;
using System.Xml.Linq;

namespace TasksList.App.Tests.Branding;

public sealed class BrandAssetTests
{
    private static readonly int[] PngSizes = [16, 24, 32, 48, 64, 128, 256, 512];
    private static readonly int[] IcoSizes = [16, 24, 32, 48, 64, 128, 256];

    [Fact]
    public void BrandAssetSetIsAccessibleAndContainsEveryWindowsIconSize()
    {
        var repository = FindRepositoryRoot();
        var brandDirectory = Path.Combine(repository, "assets", "brand");
        var generatedDirectory = Path.Combine(brandDirectory, "generated");

        foreach (var name in new[] { "logo-mark.svg", "wordmark-horizontal.svg", "github-social-preview.svg" })
        {
            var path = Path.Combine(brandDirectory, name);
            Assert.True(File.Exists(path), $"Missing SVG master: {path}");

            var svg = XDocument.Load(path);
            var root = Assert.IsType<XElement>(svg.Root);
            Assert.Equal("svg", root.Name.LocalName);
            Assert.False(string.IsNullOrWhiteSpace(root.Attribute("viewBox")?.Value));
            Assert.Contains(root.Elements(), element =>
                element.Name.LocalName == "title" && !string.IsNullOrWhiteSpace(element.Value));
        }

        foreach (var size in PngSizes)
        {
            var path = Path.Combine(generatedDirectory, $"app-icon-{size}.png");
            Assert.True(File.Exists(path), $"Missing PNG export: {path}");
            using var image = Image.FromFile(path);
            Assert.Equal(size, image.Width);
            Assert.Equal(size, image.Height);
        }

        AssertImageSize(Path.Combine(generatedDirectory, "wordmark-horizontal.png"), 1600, 420);
        AssertImageSize(Path.Combine(generatedDirectory, "github-social-preview.png"), 1280, 640);

        var icoPath = Path.Combine(generatedDirectory, "TasksList.ico");
        Assert.True(File.Exists(icoPath), $"Missing Windows icon: {icoPath}");
        Assert.Equal(IcoSizes, ReadIcoSizes(icoPath));
    }

    private static void AssertImageSize(string path, int width, int height)
    {
        Assert.True(File.Exists(path), $"Missing image export: {path}");
        using var image = Image.FromFile(path);
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    private static IReadOnlyList<int> ReadIcoSizes(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        Assert.Equal((ushort)0, reader.ReadUInt16());
        Assert.Equal((ushort)1, reader.ReadUInt16());
        var count = reader.ReadUInt16();
        var sizes = new List<int>(count);
        for (var index = 0; index < count; index++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            sizes.Add(width == 0 ? 256 : width);
            Assert.Equal(width, height);
            reader.BaseStream.Seek(14, SeekOrigin.Current);
        }

        return sizes.Order().ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
