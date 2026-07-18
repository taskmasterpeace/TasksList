using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TasksList.App.Shell;

public sealed record HotkeyRegistrationResult(bool Registered, string? ErrorMessage = null);

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private readonly nint _handle;
    private readonly HwndSource? _source;
    private readonly Dictionary<int, Action> _callbacks = [];
    private int _nextId = 0x5100;

    public GlobalHotkeyService(Window owner)
    {
        _handle = new WindowInteropHelper(owner).Handle;
        if (_handle == nint.Zero)
        {
            throw new InvalidOperationException("The library window must have a native handle before hotkeys are registered.");
        }
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WindowMessageHook);
    }

    public HotkeyRegistrationResult Register(
        AppHotkeyAction action,
        HotkeyGesture gesture,
        Action callback)
    {
        if (!gesture.IsBound)
        {
            return new HotkeyRegistrationResult(false, $"{GlobalHotkeyBindingPolicy.DisplayName(action)} has no shortcut assigned.");
        }

        var id = _nextId++;
        if (!RegisterHotKey(
                _handle,
                id,
                (uint)gesture.Modifiers | ModNoRepeat,
                gesture.VirtualKey))
        {
            var message = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return new HotkeyRegistrationResult(
                false,
                $"{GlobalHotkeyBindingPolicy.DisplayName(action)} could not use its shortcut: {message} Change it in Settings.");
        }

        _callbacks[id] = callback;
        return new HotkeyRegistrationResult(true);
    }

    public void Dispose()
    {
        foreach (var id in _callbacks.Keys)
        {
            UnregisterHotKey(_handle, id);
        }
        _callbacks.Clear();
        _source?.RemoveHook(WindowMessageHook);
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmHotkey && _callbacks.TryGetValue(wParam.ToInt32(), out var callback))
        {
            callback();
            handled = true;
        }
        return nint.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        nint window,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
