using TasksList.Core.Clipboard;
using TasksList.Core.Models;

namespace TasksList.Core.Tests.Clipboard;

public sealed class ClipboardRetentionPolicyTests
{
    [Fact]
    public void UnlimitedPolicyDoesNotDeleteItemsBasedOnCount()
    {
        var items = Enumerable.Range(0, 10_000)
            .Select(index => new RetainedCapture(
                CaptureId.New(),
                DateTimeOffset.UtcNow.AddSeconds(-index),
                1,
                false))
            .ToArray();

        var deleted = ClipboardRetentionPolicy.ChooseExpired(
            items,
            new ClipboardRetentionOptions(null, null),
            DateTimeOffset.UtcNow);

        Assert.Empty(deleted);
    }

    [Fact]
    public void ByteQuotaDeletesOldestUnpinnedItemsButPreservesFavorites()
    {
        var now = DateTimeOffset.Parse("2026-07-18T18:00:00-04:00");
        var favorite = new RetainedCapture(CaptureId.New(), now.AddHours(-4), 70, true);
        var oldest = new RetainedCapture(CaptureId.New(), now.AddHours(-3), 60, false);
        var newest = new RetainedCapture(CaptureId.New(), now.AddHours(-1), 40, false);

        var deleted = ClipboardRetentionPolicy.ChooseExpired(
            [favorite, oldest, newest],
            new ClipboardRetentionOptions(null, 110),
            now);

        Assert.Equal([oldest.Id], deleted);
    }
}
