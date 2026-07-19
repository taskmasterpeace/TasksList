using System.Windows;
using System.Windows.Media;
using TasksList.App.Theming;

namespace TasksList.App.Tests.Theming;

public sealed class WindowsThemeServiceTests
{
    [Fact]
    public void StartAndEnvironmentChangesReapplyTheSemanticPalette()
    {
        var environment = new FakeEnvironment(new WindowsThemeSnapshot(false, false, Colors.Blue));
        var sink = new FakeSink();
        using var service = new WindowsThemeService(environment, sink, ThemeLoader.Default);

        service.Start();
        var lightWindow = sink.Colors["CanvasBrush"];
        environment.Change(new WindowsThemeSnapshot(true, false, Colors.Orange));

        Assert.NotEqual(lightWindow, sink.Colors["CanvasBrush"]);
        Assert.Equal(Colors.Orange, sink.Colors["PrimaryBrush"]);
        Assert.Equal(2, sink.ApplyCount);
    }

    [Fact]
    public void CustomThemeOverridesAllowedShellTokensButNeverHighContrast()
    {
        var custom = new ThemeDefinition(
            "my-theme",
            "My theme",
            "1.0.0",
            new Dictionary<string, string>
            {
                ["canvas"] = "#123456",
                ["accent"] = "#ABCDEF",
            });
        var environment = new FakeEnvironment(new WindowsThemeSnapshot(false, false, Colors.Red));
        var sink = new FakeSink();
        using var service = new WindowsThemeService(environment, sink, custom);

        service.Start();
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), sink.Colors["CanvasBrush"]);
        Assert.Equal(Color.FromRgb(0xAB, 0xCD, 0xEF), sink.Colors["PrimaryBrush"]);

        environment.Change(new WindowsThemeSnapshot(false, true, Colors.Red));
        Assert.NotEqual(Color.FromRgb(0x12, 0x34, 0x56), sink.Colors["CanvasBrush"]);
        Assert.NotEqual(Color.FromRgb(0xAB, 0xCD, 0xEF), sink.Colors["PrimaryBrush"]);
    }

    [Fact]
    public void ResourceSinkMutatesExistingBrushSoStaticResourceConsumersStayLive()
    {
        var original = new SolidColorBrush(Colors.Black);
        var resources = new ResourceDictionary { ["CanvasBrush"] = original };
        var sink = new ApplicationThemeResourceSink(resources, null);

        sink.Apply(new Dictionary<string, Color> { ["CanvasBrush"] = Colors.White });

        Assert.Same(original, resources["CanvasBrush"]);
        Assert.Equal(Colors.White, original.Color);
    }

    private sealed class FakeEnvironment(WindowsThemeSnapshot snapshot) : IWindowsThemeEnvironment
    {
        private WindowsThemeSnapshot _snapshot = snapshot;
        public event EventHandler? Changed;
        public WindowsThemeSnapshot Read() => _snapshot;

        public void Change(WindowsThemeSnapshot snapshot)
        {
            _snapshot = snapshot;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeSink : IThemeResourceSink
    {
        public Dictionary<string, Color> Colors { get; } = [];
        public int ApplyCount { get; private set; }

        public void Apply(IReadOnlyDictionary<string, Color> colors)
        {
            ApplyCount++;
            foreach (var pair in colors) Colors[pair.Key] = pair.Value;
        }
    }
}
