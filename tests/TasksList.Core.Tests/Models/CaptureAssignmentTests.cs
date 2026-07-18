using TasksList.Core.Models;

namespace TasksList.Core.Tests.Models;

public sealed class CaptureAssignmentTests
{
    [Fact]
    public void AssigningCaptureToMultiplePlacesPreservesItsOriginalSource()
    {
        var source = ContextId.New();
        var capture = Capture.Create(
            CaptureKind.Text,
            source,
            "docker compose up",
            DateTimeOffset.Parse("2026-07-18T14:00:00-04:00"));
        var developerWorkspace = PlaceId.New();
        var dockerLater = PlaceId.New();

        var filed = capture
            .AssignTo(developerWorkspace, AssignmentActor.User)
            .AssignTo(dockerLater, AssignmentActor.User);

        Assert.Equal(source, filed.SourceContextId);
        Assert.Equal(2, filed.Assignments.Count);
        Assert.Contains(filed.Assignments, assignment => assignment.PlaceId == developerWorkspace);
        Assert.Contains(filed.Assignments, assignment => assignment.PlaceId == dockerLater);
    }

    [Fact]
    public void AssigningCaptureToTheSamePlaceTwiceDoesNotDuplicateTheAssignment()
    {
        var capture = Capture.Create(
            CaptureKind.Html,
            ContextId.New(),
            "<strong>Task'sList</strong>",
            DateTimeOffset.Parse("2026-07-18T14:30:00-04:00"));
        var place = PlaceId.New();

        var filed = capture
            .AssignTo(place, AssignmentActor.User)
            .AssignTo(place, AssignmentActor.Plugin);

        Assert.Single(filed.Assignments);
        Assert.Equal(AssignmentActor.User, filed.Assignments[0].Actor);
    }

    [Fact]
    public void CapturePreservesPlainHtmlAndRichTextRepresentations()
    {
        var capture = Capture.Create(
                CaptureKind.RichText,
                ContextId.New(),
                "Task'sList",
                DateTimeOffset.UtcNow)
            .WithTextRepresentation("text/plain", "Task'sList")
            .WithTextRepresentation("text/html", "<strong>Task'sList</strong>")
            .WithTextRepresentation("text/rtf", "{\\rtf1\\b Task'sList}");

        Assert.Equal(3, capture.TextRepresentations.Count);
        Assert.Equal("<strong>Task'sList</strong>", capture.TextRepresentations["text/html"]);
    }
}
