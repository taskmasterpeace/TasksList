using System.Text.RegularExpressions;

namespace TasksList.Core.Markdown;

public sealed record MarkdownRenderDocument(IReadOnlyList<MarkdownRenderBlock> Blocks);

public abstract record MarkdownRenderBlock;

public sealed record MarkdownHeading(int Level, string Text) : MarkdownRenderBlock;

public sealed record MarkdownParagraph(string Text) : MarkdownRenderBlock;

public sealed record MarkdownTask(bool IsChecked, string Text, int TaskIndex) : MarkdownRenderBlock;

public sealed record MarkdownCode(string Language, string Code) : MarkdownRenderBlock;

public sealed record MarkdownTable(int ColumnCount, IReadOnlyList<IReadOnlyList<string>> Rows) : MarkdownRenderBlock;

public sealed partial class MarkdownDocumentService
{
    public MarkdownRenderDocument Parse(string markdown)
    {
        var lines = Normalize(markdown).Split('\n');
        var blocks = new List<MarkdownRenderBlock>();
        var taskIndex = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || line == "---")
            {
                continue;
            }

            var fence = FenceRegex().Match(line);
            if (fence.Success)
            {
                var code = new List<string>();
                index++;
                while (index < lines.Length && !FenceRegex().IsMatch(lines[index]))
                {
                    code.Add(lines[index]);
                    index++;
                }

                blocks.Add(new MarkdownCode(fence.Groups[1].Value, string.Join('\n', code).TrimEnd()));
                continue;
            }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                blocks.Add(new MarkdownHeading(heading.Groups[1].Value.Length, heading.Groups[2].Value.Trim()));
                continue;
            }

            var task = TaskRegex().Match(line);
            if (task.Success)
            {
                blocks.Add(new MarkdownTask(
                    !string.Equals(task.Groups[1].Value, " ", StringComparison.Ordinal),
                    task.Groups[2].Value.Trim(),
                    taskIndex++));
                continue;
            }

            if (LooksLikeTableRow(line) && index + 1 < lines.Length && TableDividerRegex().IsMatch(lines[index + 1]))
            {
                var rows = new List<IReadOnlyList<string>> { ParseTableRow(line) };
                index += 2;
                while (index < lines.Length && LooksLikeTableRow(lines[index]))
                {
                    rows.Add(ParseTableRow(lines[index]));
                    index++;
                }
                index--;
                blocks.Add(new MarkdownTable(rows[0].Count, rows));
                continue;
            }

            blocks.Add(new MarkdownParagraph(line.Trim()));
        }

        return new MarkdownRenderDocument(blocks);
    }

    public string ToggleTask(string markdown, int taskIndex)
    {
        var lines = Normalize(markdown).Split('\n');
        var currentTask = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var match = TaskRegex().Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            if (currentTask == taskIndex)
            {
                var replacement = match.Groups[1].Value == " " ? "x" : " ";
                lines[index] = TaskMarkerRegex().Replace(lines[index], $"- [{replacement}]", 1);
                return string.Join('\n', lines);
            }

            currentTask++;
        }

        throw new ArgumentOutOfRangeException(nameof(taskIndex), taskIndex, "The selected Markdown task does not exist.");
    }

    public IReadOnlyList<string> SplitByTopLevelHeadings(string markdown)
    {
        var normalized = Normalize(markdown).TrimEnd();
        var lines = normalized.Split('\n');
        var headingIndexes = lines
            .Select((line, index) => (line, index))
            .Where(item => TopLevelHeadingRegex().IsMatch(item.line))
            .Select(item => item.index)
            .ToArray();

        if (headingIndexes.Length <= 1)
        {
            return [normalized];
        }

        var documents = new List<string>(headingIndexes.Length);
        for (var index = 0; index < headingIndexes.Length; index++)
        {
            var start = index == 0 ? 0 : headingIndexes[index];
            var end = index + 1 < headingIndexes.Length ? headingIndexes[index + 1] : lines.Length;
            documents.Add(string.Join('\n', lines[start..end]).Trim());
        }

        return documents;
    }

    private static string Normalize(string markdown) => markdown.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static bool LooksLikeTableRow(string line) => line.Count(character => character == '|') >= 2;

    private static IReadOnlyList<string> ParseTableRow(string line) =>
        line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    [GeneratedRegex("^```([A-Za-z0-9_+.-]*)\\s*$")]
    private static partial Regex FenceRegex();

    [GeneratedRegex("^(#{1,6})\\s+(.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("^#\\s+.+$")]
    private static partial Regex TopLevelHeadingRegex();

    [GeneratedRegex("^\\s*-\\s+\\[([ xX])\\]\\s+(.+)$")]
    private static partial Regex TaskRegex();

    [GeneratedRegex("-\\s+\\[[ xX]\\]")]
    private static partial Regex TaskMarkerRegex();

    [GeneratedRegex("^\\s*\\|?\\s*:?-{3,}:?\\s*(\\|\\s*:?-{3,}:?\\s*)+\\|?\\s*$")]
    private static partial Regex TableDividerRegex();
}

