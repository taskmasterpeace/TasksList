using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TasksList.Core.Markdown;

namespace TasksList.App.Editor;

public static class MarkdownFlowDocumentBuilder
{
    public static FlowDocument Build(
        MarkdownRenderDocument markdown,
        Action<int, bool> taskChanged)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(16, 12, 16, 18),
            FontFamily = new FontFamily("Aptos, Segoe UI"),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(58, 48, 34)),
        };

        foreach (var block in markdown.Blocks)
        {
            document.Blocks.Add(BuildBlock(block, taskChanged));
        }

        return document;
    }

    private static Block BuildBlock(MarkdownRenderBlock block, Action<int, bool> taskChanged) =>
        block switch
        {
            MarkdownHeading heading => BuildHeading(heading),
            MarkdownTask task => BuildTask(task, taskChanged),
            MarkdownCode code => BuildCode(code),
            MarkdownTable table => BuildTable(table),
            MarkdownParagraph paragraph => new Paragraph(new Run(paragraph.Text))
            {
                Margin = new Thickness(0, 3, 0, 7),
                LineHeight = 19,
            },
            _ => new Paragraph(),
        };

    private static Paragraph BuildHeading(MarkdownHeading heading) =>
        new(new Run(heading.Text))
        {
            FontSize = heading.Level switch { 1 => 23, 2 => 18, _ => 15 },
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, heading.Level == 1 ? 3 : 10, 0, 8),
            Foreground = new SolidColorBrush(Color.FromRgb(46, 39, 28)),
        };

    private static Paragraph BuildTask(MarkdownTask task, Action<int, bool> taskChanged)
    {
        var checkBox = new CheckBox
        {
            IsChecked = task.IsChecked,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Tag = task.TaskIndex,
        };
        checkBox.Checked += (_, _) => taskChanged(task.TaskIndex, true);
        checkBox.Unchecked += (_, _) => taskChanged(task.TaskIndex, false);
        var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 4) };
        paragraph.Inlines.Add(new InlineUIContainer(checkBox));
        paragraph.Inlines.Add(new Run(task.Text)
        {
            TextDecorations = task.IsChecked ? TextDecorations.Strikethrough : null,
        });
        return paragraph;
    }

    private static Paragraph BuildCode(MarkdownCode code) =>
        new(new Run(code.Code)
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
        })
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 46, 39, 28)),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 6, 0, 8),
            LineHeight = 18,
        };

    private static Table BuildTable(MarkdownTable table)
    {
        var result = new Table { CellSpacing = 0, Margin = new Thickness(0, 7, 0, 10) };
        for (var index = 0; index < table.ColumnCount; index++)
        {
            result.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = new TableRow();
            foreach (var value in table.Rows[rowIndex])
            {
                row.Cells.Add(new TableCell(new Paragraph(new Run(value)))
                {
                    Padding = new Thickness(6, 4, 6, 4),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 46, 39, 28)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    FontWeight = rowIndex == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                });
            }
            group.Rows.Add(row);
        }
        result.RowGroups.Add(group);
        return result;
    }
}
