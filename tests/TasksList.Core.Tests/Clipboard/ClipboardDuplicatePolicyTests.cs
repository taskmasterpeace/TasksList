using TasksList.Core.Clipboard;
using TasksList.Core.Models;

namespace TasksList.Core.Tests.Clipboard;

public sealed class ClipboardDuplicatePolicyTests
{
    [Fact]
    public void EquivalentFormattingRepresentationsProduceStableHash()
    {
        var first = Capture.Create(CaptureKind.Html, ContextId.New(), "Hello", DateTimeOffset.UtcNow)
            .WithTextRepresentation("text/plain", "Hello")
            .WithTextRepresentation("text/html", "<b>Hello</b>");
        var second = Capture.Create(CaptureKind.Html, ContextId.New(), "Other preview", DateTimeOffset.UtcNow)
            .WithTextRepresentation("TEXT/HTML", "<b>Hello</b>")
            .WithTextRepresentation("TEXT/PLAIN", "Hello");

        Assert.Equal(
            ClipboardDuplicatePolicy.ComputeHash(first),
            ClipboardDuplicatePolicy.ComputeHash(second));
    }

    [Fact]
    public void PromotionKeepsIdentityAndMetadataButMovesCaptureToTheTop()
    {
        var original = Capture.Create(
                CaptureKind.Text,
                ContextId.New(),
                "hello",
                DateTimeOffset.Parse("2026-07-18T10:00:00Z"))
            .WithFavorite(true);
        var copiedAgain = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

        var promoted = ClipboardDuplicatePolicy.Promote(original, copiedAgain);

        Assert.Equal(original.Id, promoted.Id);
        Assert.True(promoted.IsFavorite);
        Assert.Equal(copiedAgain, promoted.CapturedAt);
    }
}
