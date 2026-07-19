using System.Runtime.InteropServices;
using TasksList.App.Clipboard;

namespace TasksList.App.Tests.Clipboard;

public sealed class ClipboardWriteOperationTests
{
    [Fact]
    public void TransientOwnershipFailureRetriesFourTimesWithBoundedBackoff()
    {
        var attempts = 0;
        var waits = new List<TimeSpan>();

        ClipboardWriteOperation.Run(
            () =>
            {
                attempts++;
                if (attempts < 4) throw new COMException("clipboard busy");
            },
            waits.Add);

        Assert.Equal(4, attempts);
        Assert.Equal(
            [TimeSpan.FromMilliseconds(35), TimeSpan.FromMilliseconds(70), TimeSpan.FromMilliseconds(105)],
            waits);
    }

    [Fact]
    public void FinalOwnershipFailureEscapesForCaptureErrorReporting()
    {
        var attempts = 0;

        Assert.Throws<COMException>(() => ClipboardWriteOperation.Run(
            () =>
            {
                attempts++;
                throw new COMException("clipboard busy");
            },
            _ => { }));

        Assert.Equal(4, attempts);
    }
}
