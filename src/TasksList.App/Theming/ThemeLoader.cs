using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TasksList.App.Theming;

public sealed record ThemeDefinition(
    string Id,
    string Name,
    string Version,
    IReadOnlyDictionary<string, string> Tokens);

public static partial class ThemeLoader
{
    public static ThemeDefinition Default { get; } = new(
        "taskslist-default",
        "Task'sList Warm Charcoal",
        "1.0.0",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["canvas"] = "#17191B",
            ["paper"] = "#F4CE62",
            ["ink"] = "#2E271C",
            ["accent"] = "#F19A4B",
        });

    public static ThemeDefinition Load(string path)
    {
        using var stream = File.OpenRead(path);
        var theme = JsonSerializer.Deserialize<ThemeFile>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("The theme file is empty.");

        if (string.IsNullOrWhiteSpace(theme.Id) ||
            string.IsNullOrWhiteSpace(theme.Name) ||
            string.IsNullOrWhiteSpace(theme.Version) ||
            theme.Tokens is null ||
            theme.Tokens.Count == 0)
        {
            throw new InvalidDataException("The theme must declare id, name, version, and semantic tokens.");
        }

        foreach (var token in theme.Tokens)
        {
            if (!ColorRegex().IsMatch(token.Value))
            {
                throw new InvalidDataException($"Theme token '{token.Key}' is not a local hexadecimal color.");
            }
        }

        return new ThemeDefinition(theme.Id, theme.Name, theme.Version, theme.Tokens);
    }

    public static ThemeDefinition LoadOrDefault(string path)
    {
        try
        {
            return Load(path);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            return Default;
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex ColorRegex();

    private sealed record ThemeFile(
        string Id,
        string Name,
        string Version,
        Dictionary<string, string>? Tokens);
}
