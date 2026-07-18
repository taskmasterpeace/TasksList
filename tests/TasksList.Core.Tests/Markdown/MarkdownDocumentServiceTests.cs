using TasksList.Core.Markdown;

namespace TasksList.Core.Tests.Markdown;

public sealed class MarkdownDocumentServiceTests
{
    private readonly MarkdownDocumentService _service = new();

    [Fact]
    public void ParseRecognizesHeadingsTasksTablesAndFencedCode()
    {
        const string markdown = """
            # Docker cleanup

            - [ ] Save database
            - [x] Notify team

            | Container | State |
            | --- | --- |
            | api | running |

            ```powershell
            docker compose down
            ```
            """;

        var document = _service.Parse(markdown);

        var heading = Assert.IsType<MarkdownHeading>(document.Blocks[0]);
        Assert.Equal(1, heading.Level);
        Assert.Equal("Docker cleanup", heading.Text);
        Assert.Contains(document.Blocks, block => block is MarkdownTask { IsChecked: false, Text: "Save database" });
        Assert.Contains(document.Blocks, block => block is MarkdownTable { ColumnCount: 2 });
        Assert.Contains(document.Blocks, block => block is MarkdownCode { Language: "powershell", Code: "docker compose down" });
    }

    [Fact]
    public void ToggleTaskChangesOnlyTheSelectedCheckbox()
    {
        const string markdown = "- [ ] First\n- [ ] Second";

        var toggled = _service.ToggleTask(markdown, 1);

        Assert.Equal("- [ ] First\n- [x] Second", toggled);
    }

    [Fact]
    public void SplitByTopLevelHeadingsKeepsFrontMatterWithTheFirstDocument()
    {
        const string markdown = "---\ntags: [docker]\n---\n# First\nOne\n# Second\nTwo";

        var documents = _service.SplitByTopLevelHeadings(markdown);

        Assert.Equal(2, documents.Count);
        Assert.StartsWith("---\ntags: [docker]\n---\n# First", documents[0], StringComparison.Ordinal);
        Assert.Equal("# Second\nTwo", documents[1]);
    }
}

