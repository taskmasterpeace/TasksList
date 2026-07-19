using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Capture;

public static class CaptureCompletionOperation
{
    public static async Task<CaptureModel> SaveAndCopyAsync(
        Func<Task<CaptureModel>> saveCapture,
        Action<CaptureModel> copyCapture)
    {
        ArgumentNullException.ThrowIfNull(saveCapture);
        ArgumentNullException.ThrowIfNull(copyCapture);

        var storedCapture = await saveCapture();
        copyCapture(storedCapture);
        return storedCapture;
    }
}
