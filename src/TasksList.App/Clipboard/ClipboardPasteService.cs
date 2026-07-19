using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Clipboard;

public enum PasteRepresentation
{
    Original,
    PlainText,
}

public interface IClipboardPastePlatform
{
    void SetClipboard(CaptureModel capture, PasteRepresentation representation);
    void FocusWindow(nint handle);
    void SendPaste();
}

public sealed class ClipboardPasteService
{
    private readonly IClipboardPastePlatform _platform;
    private readonly Action<TimeSpan> _suppressCapture;

    public ClipboardPasteService(
        IClipboardPastePlatform platform,
        Action<TimeSpan> suppressCapture)
    {
        _platform = platform;
        _suppressCapture = suppressCapture;
    }

    public void Paste(CaptureModel capture, PasteRepresentation representation, nint targetHandle)
    {
        _suppressCapture(TimeSpan.FromSeconds(1));
        _platform.SetClipboard(capture, representation);
        _platform.FocusWindow(targetHandle);
        _platform.SendPaste();
    }

    public void Copy(CaptureModel capture, PasteRepresentation representation)
    {
        _suppressCapture(TimeSpan.FromSeconds(1));
        _platform.SetClipboard(capture, representation);
    }

    public void RestoreFocus(nint targetHandle) => _platform.FocusWindow(targetHandle);

    public void PasteJoined(
        IReadOnlyList<CaptureModel> captures,
        string separator,
        nint targetHandle)
    {
        var joined = string.Join(separator, captures.Select(capture => PlainText(capture)));
        var synthetic = CaptureModel.Create(
                CaptureKind.Text,
                captures.FirstOrDefault()?.SourceContextId ?? ContextId.New(),
                joined,
                DateTimeOffset.Now)
            .WithTextRepresentation("text/plain", joined);
        Paste(synthetic, PasteRepresentation.PlainText, targetHandle);
    }

    private static string PlainText(CaptureModel capture) =>
        capture.TextRepresentations.TryGetValue("text/plain", out var plain)
            ? plain
            : capture.PreviewText;
}

public sealed class WindowsClipboardPastePlatform : IClipboardPastePlatform
{
    private const byte VirtualControl = 0x11;
    private const byte VirtualV = 0x56;
    private const uint KeyUp = 0x0002;

    public void SetClipboard(CaptureModel capture, PasteRepresentation representation)
    {
        var data = CreateDataObject(capture, representation);
        ClipboardWriteOperation.Run(
            () => System.Windows.Clipboard.SetDataObject(data, copy: true),
            Thread.Sleep);
    }

    public static DataObject CreateDataObject(
        CaptureModel capture,
        PasteRepresentation representation)
    {
        var data = new DataObject();
        var plain = capture.TextRepresentations.TryGetValue("text/plain", out var text)
            ? text
            : capture.PreviewText;
        data.SetData(DataFormats.UnicodeText, plain);
        if (representation == PasteRepresentation.Original)
        {
            if (capture.TextRepresentations.TryGetValue("text/html", out var html))
            {
                data.SetData(DataFormats.Html, html);
            }
            if (capture.TextRepresentations.TryGetValue("text/rtf", out var rtf))
            {
                data.SetData(DataFormats.Rtf, rtf);
            }
            if (capture.TextRepresentations.TryGetValue(
                    "application/x-taskslist-payload-path",
                    out var payloadPath) && File.Exists(payloadPath))
            {
                var pngBytes = File.ReadAllBytes(payloadPath);
                using var stream = new MemoryStream(pngBytes, writable: false);
                var decoder = new PngBitmapDecoder(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var image = decoder.Frames[0];
                image.Freeze();
                data.SetData(DataFormats.Bitmap, image);
                data.SetData("PNG", pngBytes);
            }
            if (capture.TextRepresentations.TryGetValue(
                    "application/x-taskslist-files",
                    out var filesJson) &&
                JsonSerializer.Deserialize<string[]>(filesJson) is { Length: > 0 } files)
            {
                data.SetData(DataFormats.FileDrop, files);
            }
        }
        return data;
    }

    public void FocusWindow(nint handle)
    {
        if (handle != nint.Zero)
        {
            SetForegroundWindow(handle);
        }
    }

    public void SendPaste()
    {
        keybd_event(VirtualControl, 0, 0, 0);
        keybd_event(VirtualV, 0, 0, 0);
        keybd_event(VirtualV, 0, KeyUp, 0);
        keybd_event(VirtualControl, 0, KeyUp, 0);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint handle);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);
}
