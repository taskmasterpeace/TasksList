using System.Runtime.InteropServices;
using System.Windows;

namespace TasksList.App.Clipboard;

public static class ClipboardTargetReader
{
    public static nint ForegroundWindow() => GetForegroundWindow();

    public static Point CursorPosition()
    {
        GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
