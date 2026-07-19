using System.Runtime.InteropServices;

namespace TasksList.App.Clipboard;

public static class ClipboardWriteOperation
{
    public static void Run(Action write, Action<TimeSpan> wait)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(wait);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                write();
                return;
            }
            catch (COMException) when (attempt < 3)
            {
                wait(TimeSpan.FromMilliseconds(35 * (attempt + 1)));
            }
        }
    }
}
