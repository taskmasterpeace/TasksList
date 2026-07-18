using System.Windows;

namespace TasksList.App.Capture;

public static class CaptureOverlayPresentation
{
    public static void SuppressForCapture(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.IsHitTestVisible = false;
        window.Opacity = 0;
    }
}
