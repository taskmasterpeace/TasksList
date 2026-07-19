using TasksList.App.Capture;
using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureNoteFactoryTests
{
    [Fact]
    public void ImageCaptureCreatesMarkdownWithLocalImageSourceAndDimensions()
    {
        var source = ContextRef.Create(ContextKind.Application, "windows", "paint.exe", "Paint");
        var capture = CaptureModel.Create(
                CaptureKind.Image,
                source.Id,
                "Screen capture - 640 x 360",
                DateTimeOffset.UtcNow)
            .WithTextRepresentation("application/x-taskslist-payload-path", @"C:\captures\shot.png");

        var note = CaptureNoteFactory.Create(capture, source);

        Assert.Equal("Capture from Paint", note.Title);
        Assert.Contains("![Captured region](<C:\\captures\\shot.png>)", note.Markdown);
        Assert.Contains("640 x 360", note.Markdown);
        Assert.Contains(note.Attachments, item => item.ContextId == source.Id);
    }

    [Fact]
    public void TextCaptureUsesPreviewWhenNoImagePayloadExists()
    {
        var source = ContextRef.Create(ContextKind.Application, "windows", "terminal.exe", "Terminal");
        var capture = CaptureModel.Create(
            CaptureKind.Text,
            source.Id,
            "docker ps",
            DateTimeOffset.UtcNow);

        var note = CaptureNoteFactory.Create(capture, source);

        Assert.Contains("docker ps", note.Markdown);
        Assert.DoesNotContain("![Captured region]", note.Markdown);
    }
}
