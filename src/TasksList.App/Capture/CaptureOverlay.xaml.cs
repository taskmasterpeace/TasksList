using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TasksList.App.Capture;

public sealed record ScreenCaptureResult(byte[] PngBytes, int PixelWidth, int PixelHeight);

public partial class CaptureOverlay : Window
{
    private System.Windows.Point _start;
    private bool _selecting;

    public CaptureOverlay()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) => Activate();
    }

    public ScreenCaptureResult? Result { get; private set; }

    private void OverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Surface);
        _selecting = true;
        Selection.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_start);
    }

    private void OverlayMouseMove(object sender, MouseEventArgs e)
    {
        if (_selecting)
        {
            UpdateSelection(e.GetPosition(Surface));
        }
    }

    private void OverlayMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        var end = e.GetPosition(Surface);
        _selecting = false;
        ReleaseMouseCapture();
        var left = Math.Min(_start.X, end.X);
        var top = Math.Min(_start.Y, end.Y);
        var right = Math.Max(_start.X, end.X);
        var bottom = Math.Max(_start.Y, end.Y);
        if (right - left < 3 || bottom - top < 3)
        {
            DialogResult = false;
            return;
        }

        var screenTopLeft = PointToScreen(new System.Windows.Point(left, top));
        var screenBottomRight = PointToScreen(new System.Windows.Point(right, bottom));
        var pixelWidth = Math.Max(1, (int)Math.Round(screenBottomRight.X - screenTopLeft.X));
        var pixelHeight = Math.Max(1, (int)Math.Round(screenBottomRight.Y - screenTopLeft.Y));
        Visibility = Visibility.Hidden;
        Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        using var bitmap = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                (int)Math.Round(screenTopLeft.X),
                (int)Math.Round(screenTopLeft.Y),
                0,
                0,
                new System.Drawing.Size(pixelWidth, pixelHeight),
                CopyPixelOperation.SourceCopy);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        Result = new ScreenCaptureResult(stream.ToArray(), pixelWidth, pixelHeight);
        DialogResult = true;
    }

    private void UpdateSelection(System.Windows.Point current)
    {
        var left = Math.Min(_start.X, current.X);
        var top = Math.Min(_start.Y, current.Y);
        Canvas.SetLeft(Selection, left);
        Canvas.SetTop(Selection, top);
        Selection.Width = Math.Abs(current.X - _start.X);
        Selection.Height = Math.Abs(current.Y - _start.Y);
    }

    private void OverlayKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }
}
