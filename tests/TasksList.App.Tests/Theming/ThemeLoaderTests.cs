using System.IO;
using TasksList.App.Theming;

namespace TasksList.App.Tests.Theming;

public sealed class ThemeLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"taskslist-theme-tests-{Guid.NewGuid():N}");

    public ThemeLoaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void ValidThemeLoadsSemanticTokens()
    {
        var path = Path.Combine(_directory, "theme.json");
        File.WriteAllText(path, """
            {
              "id": "solar-paper",
              "name": "Solar Paper",
              "version": "1.0.0",
              "tokens": {
                "canvas": "#17191B",
                "paper": "#F4CE62",
                "ink": "#2E271C",
                "accent": "#F19A4B"
              }
            }
            """);

        var theme = ThemeLoader.Load(path);

        Assert.Equal("solar-paper", theme.Id);
        Assert.Equal("#F4CE62", theme.Tokens["paper"]);
    }

    [Fact]
    public void InvalidThemeFallsBackWithoutExecutingOrFetchingAnything()
    {
        var path = Path.Combine(_directory, "theme.json");
        File.WriteAllText(path, "{ \"id\": \"broken\", \"tokens\": { \"paper\": \"https://bad.example\" } }");

        var theme = ThemeLoader.LoadOrDefault(path);

        Assert.Equal("taskslist-default", theme.Id);
        Assert.Equal("#F4CE62", theme.Tokens["paper"]);
    }
}
