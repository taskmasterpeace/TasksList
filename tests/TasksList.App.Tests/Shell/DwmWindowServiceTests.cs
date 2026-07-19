using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class DwmWindowServiceTests
{
    [Fact]
    public void ApplyRequestsCaptionCornersAndBackdropWithoutRequiringOptionalSupport()
    {
        var platform = new FakePlatform(failingAttribute: DwmWindowService.SystemBackdropAttribute);
        var options = new DwmWindowOptions(
            UseDarkCaption: true,
            CornerPreference: DwmWindowCornerPreference.Round,
            SystemBackdrop: DwmSystemBackdropType.MainWindow);

        DwmWindowService.Apply(new nint(42), options, platform);

        Assert.Equal(
            [
                (DwmWindowService.ImmersiveDarkModeAttribute, 1),
                (DwmWindowService.CornerPreferenceAttribute, (int)DwmWindowCornerPreference.Round),
                (DwmWindowService.SystemBackdropAttribute, (int)DwmSystemBackdropType.MainWindow),
            ],
            platform.Calls);
    }

    [Fact]
    public void ApplySkipsUnavailableOptionalVisualsButStillSetsCaptionMode()
    {
        var platform = new FakePlatform();
        var options = new DwmWindowOptions(false, null, null);

        DwmWindowService.Apply(new nint(7), options, platform);

        Assert.Equal([(DwmWindowService.ImmersiveDarkModeAttribute, 0)], platform.Calls);
    }

    private sealed class FakePlatform(int? failingAttribute = null) : IDwmPlatform
    {
        public List<(int Attribute, int Value)> Calls { get; } = [];

        public bool TrySetInt32(nint windowHandle, int attribute, int value)
        {
            Calls.Add((attribute, value));
            return attribute != failingAttribute;
        }
    }
}
