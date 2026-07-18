using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using TasksList.Core.Models;

namespace TasksList.App.Clipboard;

public sealed record ClipboardSnapshot(
    CaptureKind Kind,
    string PreviewText,
    IReadOnlyDictionary<string, string> TextRepresentations,
    ContextRef Source,
    string? SourceUrl = null,
    byte[]? ImagePng = null,
    IReadOnlyList<string>? Files = null);

public sealed class ClipboardMonitor : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private readonly Window _window;
    private readonly Func<ClipboardSnapshot, Task> _onCaptured;
    private HwndSource? _source;
    private nint _handle;
    private DateTimeOffset _suppressUntil;

    public bool IsPaused { get; set; }

    public List<string> ExcludedApplications { get; set; } = [];

    public long MaximumCaptureBytes { get; set; } = 20 * 1024 * 1024;

    public void SuppressNextChange(TimeSpan duration) =>
        _suppressUntil = DateTimeOffset.Now.Add(duration);

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

                var formats = data.GetFormats(autoConvert: false).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var representations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                byte[]? imagePng = null;
                IReadOnlyList<string>? files = null;
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

                if (data.GetDataPresent(DataFormats.Bitmap) &&
                    data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = new MemoryStream();
                    encoder.Save(stream);
                    imagePng = stream.ToArray();
                }
                if (data.GetDataPresent(DataFormats.FileDrop) &&
                    data.GetData(DataFormats.FileDrop) is string[] droppedFiles)
                {
                    files = droppedFiles;
                }

                if (representations.Count == 0 && imagePng is null && files is null)
                {
                    return;
                }

                var preview = imagePng is not null
                    ? $"Image {BitmapSize(data)}"
                    : files is not null
                        ? string.Join(Environment.NewLine, files.Select(Path.GetFileName))
                        : representations.TryGetValue("text/plain", out var text)
                            ? text
                            : representations.Values.First();
                var kind = imagePng is not null
                    ? CaptureKind.Image
                    : files is not null
                        ? CaptureKind.Files
                        : representations.ContainsKey("text/rtf")
                    ? CaptureKind.RichText
                    : representations.ContainsKey("text/html")
                        ? CaptureKind.Html
                        : CaptureKind.Text;
                var source = ForegroundContextReader.Read();
                var sizeBytes = representations.Values.Sum(Encoding.UTF8.GetByteCount) +
                                (imagePng?.LongLength ?? 0) +
                                (files?.Sum(file => Encoding.UTF8.GetByteCount(file)) ?? 0);
                var candidate = new ClipboardCaptureCandidate(
                    source,
                    formats,
                    sizeBytes,
                    DateTimeOffset.Now <= _suppressUntil);
                if (!ClipboardCapturePolicy.ShouldCapture(
                        candidate,
                        new ClipboardCaptureRules(
                            IsPaused,
                            ExcludedApplications,
                            MaximumCaptureBytes)))
                {
                    return;
                }
                var sourceUrl = representations.TryGetValue("text/html", out var capturedHtml)
                    ? ExtractSourceUrl(capturedHtml)
                    : null;
                await _onCaptured(new ClipboardSnapshot(
                    kind,
                    preview,
                    representations,
                    source,
                    sourceUrl,
                    imagePng,
                    files));
                return;
            }
            catch (COMException) when (attempt < 3)
            {
                await Task.Delay(35 * (attempt + 1));
            }
        }
    }

    private static string BitmapSize(IDataObject data) =>
        data.GetData(DataFormats.Bitmap) is BitmapSource bitmap
            ? $"{bitmap.PixelWidth} × {bitmap.PixelHeight}"
            : string.Empty;

    private static string? ExtractSourceUrl(string html)
    {
        foreach (var line in html.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("SourceURL:", StringComparison.OrdinalIgnoreCase))
            {
                return line["SourceURL:".Length..].Trim();
            }
        }
        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);
}
