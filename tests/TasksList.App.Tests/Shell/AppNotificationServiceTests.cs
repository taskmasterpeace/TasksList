using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class AppNotificationServiceTests
{
    [Fact]
    public void NewNotificationReplacesThePreviousMessage()
    {
        var service = new AppNotificationService();

        service.Show("First", AppNotificationKind.Information);
        service.Show("Second", AppNotificationKind.Success);

        Assert.Equal("Second", service.Current?.Message);
        Assert.Equal(AppNotificationKind.Success, service.Current?.Kind);
    }

    [Fact]
    public async Task ActionRunsOnceAndDismissesSuccessfulNotification()
    {
        var calls = 0;
        var service = new AppNotificationService();
        service.Show(
            "Screenshot copied",
            AppNotificationKind.Success,
            "Create note",
            () =>
            {
                calls++;
                return Task.CompletedTask;
            });

        await service.InvokeActionAsync();
        await service.InvokeActionAsync();

        Assert.Equal(1, calls);
        Assert.Null(service.Current);
    }

    [Fact]
    public void DismissClearsCurrentAndPublishesChange()
    {
        var service = new AppNotificationService();
        var changes = 0;
        service.Changed += (_, _) => changes++;
        service.Show("Saved", AppNotificationKind.Success);

        service.Dismiss();

        Assert.Null(service.Current);
        Assert.Equal(2, changes);
    }
}
