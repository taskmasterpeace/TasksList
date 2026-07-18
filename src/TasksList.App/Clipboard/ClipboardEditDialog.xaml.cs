using System.Windows;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Clipboard;

public partial class ClipboardEditDialog : Window
{
    public ClipboardEditDialog(CaptureModel capture)
    {
        InitializeComponent();
        TitleBox.Text = capture.Title;
        ContentBox.Text = capture.TextRepresentations.TryGetValue("text/plain", out var plain)
            ? plain
            : capture.PreviewText;
    }

    public string ClipTitle => TitleBox.Text.Trim();
    public string ClipContent => ContentBox.Text;

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ContentBox.Text))
        {
            return;
        }
        DialogResult = true;
    }
}
