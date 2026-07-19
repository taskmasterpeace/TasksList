using TasksList.App.Capture;
using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureCompletionOperationTests
{
    [Fact]
    public async Task SaveAndCopyStoresBeforePublishingToClipboardAndReturnsStoredCapture()
    {
        var events = new List<string>();
        var stored = CaptureModel.Create(
            CaptureKind.Image,
            ContextId.New(),
            "Screen capture · 20 × 10",
            DateTimeOffset.Parse("2026-07-18T22:00:00-04:00"));

        var result = await CaptureCompletionOperation.SaveAndCopyAsync(
            () =>
            {
                events.Add("save");
                return Task.FromResult(stored);
            },
            capture => events.Add($"copy:{capture.Id}"));

        Assert.Same(stored, result);
        Assert.Equal(["save", $"copy:{stored.Id}"], events);
    }

    [Fact]
    public async Task SaveFailureNeverPublishesAnUnstoredCapture()
    {
        var copied = false;

        await Assert.ThrowsAsync<IOException>(() =>
            CaptureCompletionOperation.SaveAndCopyAsync(
                () => Task.FromException<CaptureModel>(new IOException("disk full")),
                _ => copied = true));

        Assert.False(copied);
    }
}
