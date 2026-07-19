using TasksList.App.Shell;
using TasksList.Core.Models;

namespace TasksList.App.Tests.Shell;

public sealed class WindowsNotificationPolicyTests
{
    [Fact]
    public void ReminderRequestUsesReadablePreviewAndStableNoteArguments()
    {
        var note = Note.Create(
            "Ship release",
            "# Ship release\n\n- [ ] Run tests\n- [ ] Publish the installer\n\n" + new string('x', 240));

        var request = WindowsNotificationPolicy.ForReminder(note);

        Assert.Equal("Reminder: Ship release", request.Title);
        Assert.DoesNotContain('#', request.Message);
        Assert.DoesNotContain("- [ ]", request.Message, StringComparison.Ordinal);
        Assert.InRange(request.Message.Length, 1, WindowsNotificationPolicy.MaximumPreviewLength);
        Assert.Equal("open-note", request.Arguments["action"]);
        Assert.Equal(note.Id.Value.ToString("D"), request.Arguments["noteId"]);
        Assert.Equal("Open note", request.ActionLabel);
    }

    [Fact]
    public void ActivationPolicyAcceptsOnlyOpenNoteWithGuid()
    {
        var noteId = NoteId.New();

        var activation = WindowsNotificationPolicy.ParseActivation(new Dictionary<string, string>
        {
            ["action"] = "open-note",
            ["noteId"] = noteId.Value.ToString("D"),
        });

        Assert.Equal(noteId, activation?.NoteId);
        Assert.Null(WindowsNotificationPolicy.ParseActivation(new Dictionary<string, string>()));
        Assert.Null(WindowsNotificationPolicy.ParseActivation(new Dictionary<string, string>
        {
            ["action"] = "delete-note",
            ["noteId"] = noteId.Value.ToString("D"),
        }));
    }

    [Fact]
    public void AppRegistersBeforeShowingAndUnregistersOnExit()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root, "src", "TasksList.App", "Shell", "WindowsAppNotificationService.cs"));
        var app = File.ReadAllText(Path.Combine(root, "src", "TasksList.App", "App.xaml.cs"));
        var main = File.ReadAllText(Path.Combine(root, "src", "TasksList.App", "MainWindow.xaml.cs"));

        Assert.Contains("AppNotificationManager.Default", service, StringComparison.Ordinal);
        Assert.True(
            service.IndexOf("NotificationInvoked +=", StringComparison.Ordinal) <
            service.IndexOf(".Register(", StringComparison.Ordinal));
        Assert.Contains("_windowsNotifications?.Unregister()", app, StringComparison.Ordinal);
        Assert.Contains("--unregister-notifications", app, StringComparison.Ordinal);
        Assert.Contains("TryShowReminder(note)", main, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
