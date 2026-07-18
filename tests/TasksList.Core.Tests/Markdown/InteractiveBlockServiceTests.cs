using TasksList.Core.Markdown;

namespace TasksList.Core.Tests.Markdown;

public sealed class InteractiveBlockServiceTests
{
    private readonly InteractiveBlockService _service = new();

    [Fact]
    public void ParsesProgressCounterAndTimerWithEscapedLabels()
    {
        const string markdown = """
            :::progress value=65 label="Release \"candidate\""
            :::counter value=3 label="Attempts"
            :::timer minutes=25 label="Focus"
            """;

        var blocks = _service.Parse(markdown);

        Assert.Equal(3, blocks.Count);
        Assert.Equal(65, Assert.IsType<ProgressInteractiveBlock>(blocks[0]).Value);
        Assert.Equal("Release \"candidate\"", blocks[0].Label);
        Assert.Equal(3, Assert.IsType<CounterInteractiveBlock>(blocks[1]).Value);
        Assert.Equal(25, Assert.IsType<TimerInteractiveBlock>(blocks[2]).Minutes);
    }

    [Fact]
    public void UpdatesOnlyTheSelectedAttributeAndPreservesEverythingElseByteForByte()
    {
        const string markdown = "# Plan\r\n\r\n:::progress value=65 label=\"Release\"\r\nKeep 65 here.";

        var updated = _service.SetProgress(markdown, 0, 82);

        Assert.Equal("# Plan\r\n\r\n:::progress value=82 label=\"Release\"\r\nKeep 65 here.", updated);
    }

    [Fact]
    public void ValuesAreBoundedAndMalformedDirectivesRemainPlainMarkdown()
    {
        const string malformed = ":::progress value=nope label=\"Bad\"";

        Assert.Empty(_service.Parse(malformed));
        Assert.Equal(100, Assert.IsType<ProgressInteractiveBlock>(
            _service.Parse(":::progress value=999 label=\"High\"").Single()).Value);
        Assert.Equal(1, Assert.IsType<TimerInteractiveBlock>(
            _service.Parse(":::timer minutes=0 label=\"Timer\"").Single()).Minutes);
    }

    [Fact]
    public void CounterAndTimerEditsAddressBlocksByTypeIndex()
    {
        const string markdown = ":::counter value=1 label=\"A\"\n:::counter value=9 label=\"B\"\n:::timer minutes=25 label=\"Focus\"";

        var counter = _service.SetCounter(markdown, 1, 10);
        var timer = _service.SetTimerDuration(counter, 0, 50);

        Assert.Contains("value=1", timer);
        Assert.Contains("value=10", timer);
        Assert.Contains("minutes=50", timer);
    }
}
