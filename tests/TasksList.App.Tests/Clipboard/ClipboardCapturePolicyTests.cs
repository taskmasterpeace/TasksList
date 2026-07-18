using TasksList.App.Clipboard;
using TasksList.Core.Models;

namespace TasksList.App.Tests.Clipboard;

public sealed class ClipboardCapturePolicyTests
{
    private static readonly ContextRef Source = new(
        ContextId.New(),
        ContextKind.Application,
        "windows",
        @"C:\Apps\KeePass.exe|Database",
        "KeePass");

    [Fact]
    public void PauseExclusionPrivateFormatSelfPasteAndSizeAreRejected()
    {
        var normal = new ClipboardCaptureCandidate(Source, ["UnicodeText"], 100, false);

        Assert.False(ClipboardCapturePolicy.ShouldCapture(normal,
            new ClipboardCaptureRules(true, [], 1000)));
        Assert.False(ClipboardCapturePolicy.ShouldCapture(normal,
            new ClipboardCaptureRules(false, ["KeePass.exe"], 1000)));
        Assert.False(ClipboardCapturePolicy.ShouldCapture(normal with
        {
            Formats = ["UnicodeText", "ExcludeClipboardContentFromMonitorProcessing"],
        }, new ClipboardCaptureRules(false, [], 1000)));
        Assert.False(ClipboardCapturePolicy.ShouldCapture(normal with { IsSelfPaste = true },
            new ClipboardCaptureRules(false, [], 1000)));
        Assert.False(ClipboardCapturePolicy.ShouldCapture(normal with { SizeBytes = 1001 },
            new ClipboardCaptureRules(false, [], 1000)));
        Assert.True(ClipboardCapturePolicy.ShouldCapture(normal,
            new ClipboardCaptureRules(false, [], 1000)));
    }
}
