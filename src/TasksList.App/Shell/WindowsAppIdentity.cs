using System.Runtime.InteropServices;

namespace TasksList.App.Shell;

public static class WindowsAppIdentity
{
    public const string AppUserModelId = "TaskMasterPeace.TasksList";

    public static string? TryApply()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppUserModelId));
            return null;
        }
        catch (Exception exception)
        {
            return $"Windows could not apply Task'sList shell identity: {exception.Message}";
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
