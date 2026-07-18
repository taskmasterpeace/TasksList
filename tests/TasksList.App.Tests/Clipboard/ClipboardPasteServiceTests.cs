using TasksList.App.Clipboard;
using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Tests.Clipboard;

public sealed class ClipboardPasteServiceTests
{
    [Fact]
    public void PasteSuppressesCaptureSetsRepresentationRestoresFocusAndSendsPasteInOrder()
    {
        var platform = new FakePlatform();
        var service = new ClipboardPasteService(platform, duration => platform.Events.Add("suppress"));
        var capture = CaptureModel.Create(CaptureKind.Html, ContextId.New(), "Hello", DateTimeOffset.UtcNow)
            .WithTextRepresentation("text/plain", "Hello")
            .WithTextRepresentation("text/html", "<b>Hello</b>");

        service.Paste(capture, PasteRepresentation.PlainText, new nint(42));

        Assert.Equal(["suppress", "set:PlainText:Hello", "focus:42", "paste"], platform.Events);
    }

    [Fact]
    public void JoinedPasteUsesSelectedItemsInDisplayedOrder()
    {
        var platform = new FakePlatform();
        var service = new ClipboardPasteService(platform, _ => { });
        var captures = new[]
        {
            CaptureModel.Create(CaptureKind.Text, ContextId.New(), "one", DateTimeOffset.UtcNow),
            CaptureModel.Create(CaptureKind.Text, ContextId.New(), "two", DateTimeOffset.UtcNow),
        };

        service.PasteJoined(captures, "\n---\n", new nint(7));

        Assert.Contains("set:PlainText:one\n---\ntwo", platform.Events);
    }

    private sealed class FakePlatform : IClipboardPastePlatform
    {
        public List<string> Events { get; } = [];
        public void SetClipboard(CaptureModel capture, PasteRepresentation representation) =>
            Events.Add($"set:{representation}:{capture.PreviewText}");
        public void FocusWindow(nint handle) => Events.Add($"focus:{handle}");
        public void SendPaste() => Events.Add("paste");
    }
}
