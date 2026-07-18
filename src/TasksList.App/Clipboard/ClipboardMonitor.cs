using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TasksList.Core.Models;

namespace TasksList.App.Clipboard;

public sealed record ClipboardSnapshot(
    CaptureKind Kind,
    string PreviewText,
    IReadOnlyDictionary<string, string> TextRepresentations,
    ContextRef Source);

public sealed class ClipboardMonitor : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private readonly Window _window;
    private readonly Func<ClipboardSnapshot, Task> _onCaptured;
    private HwndSource? _source;
    private nint _handle;

    public bool IsPaused { get; set; }

    public ClipboardMonitor(Window window, Func<ClipboardSnapshot, Task> onCaptured)
    {
        _window = window;
        _onCaptured = onCaptured;
        _window.SourceInitialized += OnSourceInitialized;
        if (new WindowInteropHelper(window).Handle is { } handle && handle != nint.Zero)
        {
            Start(handle);
        }
    }

    public void Dispose()
    {
        _window.SourceInitialized -= OnSourceInitialized;
        if (_source is not null)
        {
            _source.RemoveHook(WindowMessageHook);
            _source = null;
        }
        if (_handle != nint.Zero)
        {
            RemoveClipboardFormatListener(_handle);
            _handle = nint.Zero;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        Start(new WindowInteropHelper(_window).Handle);

    private void Start(nint handle)
    {
        if (_handle != nint.Zero)
        {
            return;
        }

        _handle = handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowMessageHook);
        if (!AddClipboardFormatListener(handle))
        {
            throw new InvalidOperationException("Windows refused the clipboard listener registration.");
        }
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmClipboardUpdate)
        {
            if (IsPaused)
            {
                return nint.Zero;
            }
            _window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(async () => await ReadClipboardAsync()));
        }
        return nint.Zero;
    }

    private async Task ReadClipboardAsync()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                var data = System.Windows.Clipboard.GetDataObject();
                if (data is null)
                {
                    return;
                }

                var representations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (data.GetDataPresent(DataFormats.UnicodeText) && data.GetData(DataFormats.UnicodeText) is string plain)
                {
                    representations["text/plain"] = plain;
                }
                if (data.GetDataPresent(DataFormats.Html) && data.GetData(DataFormats.Html) is string html)
                {
                    representations["text/html"] = html;
                }
                if (data.GetDataPresent(DataFormats.Rtf) && data.GetData(DataFormats.Rtf) is string rtf)
                {
                    representations["text/rtf"] = rtf;
                }

                if (representations.Count == 0)
                {
                    return;
                }

                var preview = representations.TryGetValue("text/plain", out var text)
                    ? text
                    : representations.Values.First();
                var kind = representations.ContainsKey("text/rtf")
                    ? CaptureKind.RichText
                    : representations.ContainsKey("text/html")
                        ? CaptureKind.Html
                        : CaptureKind.Text;
                var source = ForegroundContextReader.Read();
                await _onCaptured(new ClipboardSnapshot(kind, preview, representations, source));
                return;
            }
            catch (COMException) when (attempt < 3)
            {
                await Task.Delay(35 * (attempt + 1));
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);
}
