using System.Windows.Documents;
using System.Windows.Media;
using TasksList.App.Editor;
using TasksList.Core.Markdown;
using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.App.Tests.Editor;

public sealed class MarkdownFlowDocumentBuilderTests
{
    [Fact]
    public void GraphitePresentationSuppliesReadablePreviewInk()
    {
        var presentation = NotePresentation.Default(NoteId.New()).ApplyStyle(
            NoteStyle.FromPreset(PaperPreset.Graphite));
        var markdown = new MarkdownDocumentService().Parse("# Heading\n\nBody");

        var document = MarkdownFlowDocumentBuilder.Build(
            markdown,
            (_, _) => { },
            presentation: presentation);

        var foreground = Assert.IsType<SolidColorBrush>(document.Foreground);
        var heading = Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
        var headingInk = Assert.IsType<SolidColorBrush>(heading.Foreground);
        Assert.Equal("#FFF4EFE6", foreground.Color.ToString());
        Assert.Equal(foreground.Color, headingInk.Color);
    }
}
