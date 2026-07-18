using TasksList.Core.Models;
using TasksList.Infrastructure.Storage;

namespace TasksList.Infrastructure.Tests.Storage;

public sealed class TasksListDatabaseTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"taskslist-db-tests-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_directory, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task NoteRoundTripsWithMarkdownAndAttachment()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "taskslist.db"));
        await database.InitializeAsync();
        var context = ContextRef.Create(
            ContextKind.Application,
            "windows",
            "docker-desktop",
            "Docker Desktop");
        var note = Note.Create("Docker later", "# Cleanup\n\n- [ ] Prune images")
            .AttachTo(context.Id, AttachmentVisibility.ForegroundOnly);

        await database.SaveContextAsync(context);
        await database.SaveNoteAsync(note);
        var loaded = await database.GetNoteAsync(note.Id);

        Assert.NotNull(loaded);
        Assert.Equal(note.Id, loaded.Id);
        Assert.Equal(note.Markdown, loaded.Markdown);
        Assert.Equal(context.Id, Assert.Single(loaded.Attachments).ContextId);
    }

    [Fact]
    public async Task CaptureSearchReturnsSourceAndAllFiledPlaces()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "taskslist.db"));
        await database.InitializeAsync();
        var source = ContextRef.Create(
            ContextKind.BrowserTab,
            "browser-context",
            "https://docs.docker.com/reference/cli/docker/system/prune/",
            "Docker prune documentation");
        var firstPlace = Place.Create(PlaceKind.Project, "Task'sList", null, "repo:taskslist");
        var secondPlace = Place.Create(PlaceKind.ManualGroup, "Docker later", null, "manual:docker-later");
        var capture = Capture.Create(
                CaptureKind.Text,
                source.Id,
                "docker system prune removes unused data",
                DateTimeOffset.Parse("2026-07-18T16:00:00-04:00"))
            .AssignTo(firstPlace.Id, AssignmentActor.User)
            .AssignTo(secondPlace.Id, AssignmentActor.User);

        await database.SaveContextAsync(source);
        await database.SavePlaceAsync(firstPlace);
        await database.SavePlaceAsync(secondPlace);
        await database.SaveCaptureAsync(capture);
        var matches = await database.SearchCapturesAsync("unused data", 20);

        var loaded = Assert.Single(matches);
        var loadedSource = await database.GetContextAsync(loaded.SourceContextId);
        Assert.Equal(source.Id, loaded.SourceContextId);
        Assert.Equal("Docker prune documentation", loadedSource?.DisplayName);
        Assert.Equal(2, loaded.Assignments.Count);
    }

    [Fact]
    public async Task ListNotesReturnsAllSavedNotesInTitleOrder()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "taskslist.db"));
        await database.InitializeAsync();
        await database.SaveNoteAsync(Note.Create("Zebra", "last"));
        await database.SaveNoteAsync(Note.Create("Alpha", "first"));

        var notes = await database.ListNotesAsync();

        Assert.Equal(new[] { "Alpha", "Zebra" }, notes.Select(note => note.Title));
    }

    [Fact]
    public async Task ListCapturesReturnsEverySavedItemNewestFirst()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "taskslist.db"));
        await database.InitializeAsync();
        var source = ContextRef.Create(ContextKind.Application, "windows", "test-app", "Test App");
        await database.SaveContextAsync(source);
        for (var index = 0; index < 250; index++)
        {
            await database.SaveCaptureAsync(Capture.Create(
                    CaptureKind.Text,
                    source.Id,
                    $"clip {index}",
                    DateTimeOffset.Parse("2026-07-18T18:00:00-04:00").AddSeconds(index))
                .WithTextRepresentation("text/plain", $"clip {index}"));
        }

        var captures = await database.ListCapturesAsync();

        Assert.Equal(250, captures.Count);
        Assert.Equal("clip 249", captures[0].PreviewText);
        Assert.Equal("clip 249", captures[0].TextRepresentations["text/plain"]);
        Assert.Equal("clip 0", captures[^1].PreviewText);
    }

    [Fact]
    public async Task BrowserSessionRoundTripsWithManualHierarchyAndDuplicateTabs()
    {
        var database = new TasksListDatabase(Path.Combine(_directory, "taskslist.db"));
        await database.InitializeAsync();
        var browser = Place.Create(PlaceKind.Browser, "Microsoft Edge", null, "edge");
        var manual = Place.Create(PlaceKind.ManualGroup, "Research", browser.Id, "manual:research");
        var sessionPlace = Place.Create(PlaceKind.BrowserSession, "Friday tabs", manual.Id, "session:friday");
        var tabs = new[]
        {
            SavedTab.Create(sessionPlace.Id, "https://example.com", "Example", 0, 0),
            SavedTab.Create(sessionPlace.Id, "https://example.com", "Example copy", 0, 1),
        };

        await database.SavePlaceAsync(browser);
        await database.SavePlaceAsync(manual);
        await database.SaveBrowserSessionAsync(sessionPlace, tabs);
        var places = await database.ListPlacesAsync();
        var loadedTabs = await database.ListSavedTabsAsync(sessionPlace.Id);

        Assert.Equal(3, places.Count);
        Assert.Equal(browser.Id, places.Single(place => place.Id == manual.Id).ParentId);
        Assert.Equal(2, loadedTabs.Count);
        Assert.Equal(new[] { 0, 1 }, loadedTabs.Select(tab => tab.TabIndex));
    }
}
