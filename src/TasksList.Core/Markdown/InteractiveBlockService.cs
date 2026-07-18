using System.Text.RegularExpressions;

namespace TasksList.Core.Markdown;

public abstract record InteractiveBlock(
    int TypeIndex,
    string Label,
    int SourceStart,
    int SourceLength);

public sealed record ProgressInteractiveBlock(
    int TypeIndex,
    string Label,
    int Value,
    int SourceStart,
    int SourceLength) : InteractiveBlock(TypeIndex, Label, SourceStart, SourceLength);

public sealed record CounterInteractiveBlock(
    int TypeIndex,
    string Label,
    int Value,
    int SourceStart,
    int SourceLength) : InteractiveBlock(TypeIndex, Label, SourceStart, SourceLength);

public sealed record TimerInteractiveBlock(
    int TypeIndex,
    string Label,
    int Minutes,
    int SourceStart,
    int SourceLength) : InteractiveBlock(TypeIndex, Label, SourceStart, SourceLength);

public sealed partial class InteractiveBlockService
{
    public IReadOnlyList<InteractiveBlock> Parse(string markdown)
    {
        var blocks = new List<InteractiveBlock>();
        var typeIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in DirectiveRegex().Matches(markdown))
        {
            var type = match.Groups[1].Value.ToLowerInvariant();
            var attributes = ParseAttributes(match.Groups[2].Value);
            var typeIndex = typeIndexes.GetValueOrDefault(type);
            if (TryCreate(type, typeIndex, attributes, match.Index, match.Length) is { } block)
            {
                blocks.Add(block);
                typeIndexes[type] = typeIndex + 1;
            }
        }
        return blocks;
    }

    public string SetProgress(string markdown, int index, int value) =>
        ReplaceNumericAttribute(
            markdown,
            Parse(markdown).OfType<ProgressInteractiveBlock>().ElementAtOrDefault(index),
            "value",
            Math.Clamp(value, 0, 100));

    public string SetCounter(string markdown, int index, int value) =>
        ReplaceNumericAttribute(
            markdown,
            Parse(markdown).OfType<CounterInteractiveBlock>().ElementAtOrDefault(index),
            "value",
            Math.Clamp(value, -999_999, 999_999));

    public string SetTimerDuration(string markdown, int index, int minutes) =>
        ReplaceNumericAttribute(
            markdown,
            Parse(markdown).OfType<TimerInteractiveBlock>().ElementAtOrDefault(index),
            "minutes",
            Math.Clamp(minutes, 1, 1440));

    public static InteractiveBlock? ParseLine(
        string line,
        IReadOnlyDictionary<string, int>? typeIndexes = null)
    {
        var match = DirectiveRegex().Match(line);
        if (!match.Success || match.Index != 0 || match.Length != line.Length)
        {
            return null;
        }
        var type = match.Groups[1].Value.ToLowerInvariant();
        var index = typeIndexes?.GetValueOrDefault(type) ?? 0;
        return TryCreate(type, index, ParseAttributes(match.Groups[2].Value), 0, line.Length);
    }

    private static InteractiveBlock? TryCreate(
        string type,
        int typeIndex,
        IReadOnlyDictionary<string, string> attributes,
        int sourceStart,
        int sourceLength)
    {
        var label = attributes.GetValueOrDefault("label") ?? type;
        return type switch
        {
            "progress" when TryInteger(attributes, "value", out var progress) =>
                new ProgressInteractiveBlock(typeIndex, label, Math.Clamp(progress, 0, 100), sourceStart, sourceLength),
            "counter" when TryInteger(attributes, "value", out var counter) =>
                new CounterInteractiveBlock(typeIndex, label, Math.Clamp(counter, -999_999, 999_999), sourceStart, sourceLength),
            "timer" when TryInteger(attributes, "minutes", out var minutes) =>
                new TimerInteractiveBlock(typeIndex, label, Math.Clamp(minutes, 1, 1440), sourceStart, sourceLength),
            _ => null,
        };
    }

    private static bool TryInteger(
        IReadOnlyDictionary<string, string> attributes,
        string name,
        out int value) =>
        int.TryParse(attributes.GetValueOrDefault(name), out value);

    private static Dictionary<string, string> ParseAttributes(string source)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex().Matches(source))
        {
            var value = match.Groups["quoted"].Success
                ? Regex.Unescape(match.Groups["quoted"].Value)
                : match.Groups["bare"].Value;
            attributes[match.Groups["name"].Value] = value;
        }
        return attributes;
    }

    private static string ReplaceNumericAttribute(
        string markdown,
        InteractiveBlock? block,
        string attribute,
        int value)
    {
        if (block is null)
        {
            throw new ArgumentOutOfRangeException(nameof(block), "The selected interactive block does not exist.");
        }
        var directive = markdown.Substring(block.SourceStart, block.SourceLength);
        var pattern = $@"(?<=\b{Regex.Escape(attribute)}=)-?\d+";
        var updated = new Regex(pattern).Replace(directive, value.ToString(), 1);
        return string.Concat(
            markdown.AsSpan(0, block.SourceStart),
            updated,
            markdown.AsSpan(block.SourceStart + block.SourceLength));
    }

    [GeneratedRegex("(?m)^:::(progress|counter|timer)\\s+([^\\r\\n]+)\\r?$", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex("(?<name>[A-Za-z][A-Za-z0-9_-]*)=(?:\"(?<quoted>(?:\\\\.|[^\"])*)\"|(?<bare>\\S+))")]
    private static partial Regex AttributeRegex();
}
