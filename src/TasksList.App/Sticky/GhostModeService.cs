using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TasksList.App.Sticky;

public static class GhostModeService
{
    private const int ExtendedStyleIndex = -20;
    private const long Layered = 0x00080000L;
    private const long Transparent = 0x00000020L;

    public static void SetGhost(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        var updated = enabled
            ? style | Layered | Transparent
            : style & ~Transparent;
        SetWindowLongPtr(handle, ExtendedStyleIndex, new nint(updated));
    }

    private static nint GetWindowLongPtr(nint handle, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, index)
            : new nint(GetWindowLong32(handle, index));

    private static nint SetWindowLongPtr(nint handle, int index, nint value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, index, value)
            : new nint(SetWindowLong32(handle, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(nint handle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr64(nint handle, int index, nint value);
}
