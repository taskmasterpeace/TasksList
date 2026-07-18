using TasksList.App.Capture;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureOperationTests
{
    [Fact]
    public async Task RunAsyncContainsAnUnexpectedFailureAndReportsAUsefulMessage()
    {
        var messages = new List<string>();

        var succeeded = await CaptureOperation.RunAsync(
            () => Task.FromException(new InvalidOperationException("graphics copy failed")),
            messages.Add);

        Assert.False(succeeded);
        var message = Assert.Single(messages);
        Assert.Contains("Screen capture failed", message);
        Assert.Contains("Task'sList is still running", message);
        Assert.Contains("graphics copy failed", message);
    }

    [Fact]
    public async Task RunAsyncReturnsSuccessWithoutReportingWhenWorkCompletes()
    {
        var messages = new List<string>();

        var succeeded = await CaptureOperation.RunAsync(
            () => Task.CompletedTask,
            messages.Add);

        Assert.True(succeeded);
        Assert.Empty(messages);
    }
}
