using System.Text.Json;
using System.Text.RegularExpressions;

namespace TasksList.PluginSdk;

public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    int ApiVersion,
    string EntryPoint,
    IReadOnlyList<PluginCapability> Capabilities)
{
    public static PluginManifest Parse(string json)
    {
        var file = JsonSerializer.Deserialize<ManifestFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("The plugin manifest is empty.");
        return new PluginManifest(
            file.Id ?? string.Empty,
            file.Name ?? string.Empty,
            file.Version ?? string.Empty,
            file.ApiVersion,
            file.EntryPoint ?? string.Empty,
            (file.Capabilities ?? []).Select(PluginCapabilityNames.Parse).ToArray());
    }

    private sealed record ManifestFile(
        string? Id,
        string? Name,
        string? Version,
        int ApiVersion,
        string? EntryPoint,
        string[]? Capabilities);
}

public static partial class PluginManifestValidator
{
    public static void Validate(PluginManifest manifest, int supportedApiVersion)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id) ||
            string.IsNullOrWhiteSpace(manifest.Name) ||
            !SemanticVersionRegex().IsMatch(manifest.Version))
        {
            throw new InvalidDataException("The plugin manifest must have an id, name, and semantic version.");
        }

        if (manifest.ApiVersion != supportedApiVersion)
        {
            throw new InvalidDataException(
                $"Plugin API {manifest.ApiVersion} is unsupported; this host supports API {supportedApiVersion}.");
        }

        var normalizedEntryPoint = manifest.EntryPoint.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedEntryPoint) ||
            Path.IsPathRooted(normalizedEntryPoint) ||
            normalizedEntryPoint.Split('/').Any(segment => segment == ".."))
        {
            throw new InvalidDataException("The plugin entry point must stay inside its package.");
        }
    }

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[A-Za-z0-9.-]+)?$")]
    private static partial Regex SemanticVersionRegex();
}

