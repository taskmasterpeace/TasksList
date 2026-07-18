using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TasksList.Core.Models;

namespace TasksList.App.Clipboard;

internal static class ForegroundContextReader
{
    public static ContextRef Read()
    {
        var handle = GetForegroundWindow();
        GetWindowThreadProcessId(handle, out var processId);
        var title = GetWindowTitle(handle);
        string processPath;
        string processName;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processPath = process.MainModule?.FileName ?? process.ProcessName;
            processName = process.ProcessName;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            processPath = "unknown";
            processName = "Unknown application";
        }

        var stableIdentity = $"{processPath}|{title}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableIdentity.ToUpperInvariant()));
        var contextId = new ContextId(new Guid(hash.AsSpan(0, 16)));
        var displayName = string.IsNullOrWhiteSpace(title) ? processName : $"{processName} · {title}";
        return new ContextRef(contextId, ContextKind.Window, "windows", stableIdentity, displayName);
    }

    private static string GetWindowTitle(nint handle)
    {
        var length = GetWindowTextLength(handle);
        var builder = new StringBuilder(Math.Max(length + 1, 2));
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hwnd);
}
