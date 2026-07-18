using System.Runtime.ExceptionServices;
using System.Windows;
using TasksList.App.Capture;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureOverlayPresentationTests
{
    [Fact]
    public void SuppressForCaptureMakesTheWindowTransparentWithoutEndingItsVisibleState()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                window = new Window
                {
                    Width = 120,
                    Height = 80,
                    Opacity = 1,
                    IsHitTestVisible = true,
                };
                window.Show();
                var visibility = window.Visibility;

                CaptureOverlayPresentation.SuppressForCapture(window);

                Assert.Equal(visibility, window.Visibility);
                Assert.Equal(0, window.Opacity);
                Assert.False(window.IsHitTestVisible);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                window?.Close();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "WPF presentation test timed out.");

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
