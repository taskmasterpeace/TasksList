using TasksList.Core.Clipboard;
using TasksList.Core.Models;

namespace TasksList.Core.Tests.Clipboard;

public sealed class ClipboardQueryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void CombinedFiltersMatchFavoriteUsedUnfiledSourceTypeAndDate()
    {
        var source = ContextId.New();
        var capture = Capture.Create(CaptureKind.Html, source, "Docker compose", Now)
            .WithFavorite(true)
            .MarkUsed(Now.AddMinutes(1));
        var query = new ClipboardQuery
        {
            Text = "compose",
            Favorite = true,
            Used = true,
            Unfiled = true,
            SourceContextIds = [source],
            Kinds = [CaptureKind.Html],
            CapturedAfter = Now.AddMinutes(-1),
            CapturedBefore = Now.AddMinutes(1),
        };

        Assert.True(query.Matches(capture));
        Assert.False((query with { Kinds = [CaptureKind.Image] }).Matches(capture));
        Assert.False((query with { Text = "kubernetes" }).Matches(capture));
    }

    [Fact]
    public void DeletedCapturesAreExcludedUnlessExplicitlyRequested()
    {
        var capture = Capture.Create(CaptureKind.Text, ContextId.New(), "secret", Now)
            .SoftDelete(Now);

        Assert.False(new ClipboardQuery().Matches(capture));
        Assert.True(new ClipboardQuery { IncludeDeleted = true }.Matches(capture));
    }

    [Fact]
    public void AssignmentToANoteMeansTheClipIsFiled()
    {
        var capture = Capture.Create(CaptureKind.Text, ContextId.New(), "file me", Now)
            .AssignToNote(NoteId.New());

        Assert.False(new ClipboardQuery { Unfiled = true }.Matches(capture));
        Assert.True(new ClipboardQuery { Unfiled = false }.Matches(capture));
    }
}
