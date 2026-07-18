using System.Runtime.InteropServices;
using System.Windows;

namespace TasksList.App.Sticky;

public static class MonitorWorkAreaProvider
{
    public static IReadOnlyList<WindowBounds> GetWorkAreas()
    {
        var workAreas = new List<WindowBounds>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            var scale = GetScale(monitor);
            workAreas.Add(new WindowBounds(
                info.WorkArea.Left / scale,
                info.WorkArea.Top / scale,
                (info.WorkArea.Right - info.WorkArea.Left) / scale,
                (info.WorkArea.Bottom - info.WorkArea.Top) / scale));
            return true;
        }, IntPtr.Zero);

        if (workAreas.Count == 0)
        {
            var fallback = SystemParameters.WorkArea;
            workAreas.Add(new WindowBounds(
                fallback.Left,
                fallback.Top,
                fallback.Width,
                fallback.Height));
        }

        return workAreas;
    }

    private static double GetScale(IntPtr monitor)
    {
        try
        {
            return GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0
                ? dpiX / 96d
                : 1d;
        }
        catch (DllNotFoundException)
        {
            return 1d;
        }
        catch (EntryPointNotFoundException)
        {
            return 1d;
        }
    }

    private delegate bool MonitorEnumProc(
        IntPtr monitor,
        IntPtr deviceContext,
        IntPtr monitorRect,
        IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRect,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
