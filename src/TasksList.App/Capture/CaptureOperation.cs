namespace TasksList.App.Capture;

public static class CaptureOperation
{
    public static async Task<bool> RunAsync(Func<Task> operation, Action<string> reportError)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(reportError);

        try
        {
            await operation();
            return true;
        }
        catch (Exception exception)
        {
            reportError(
                "Screen capture failed, but Task'sList is still running.\n\n" +
                exception.Message);
            return false;
        }
    }
}
