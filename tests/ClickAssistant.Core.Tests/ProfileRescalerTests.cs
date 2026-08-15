using ClickAssistant.Core.Models;
using ClickAssistant.Core.Screen;
using Xunit;

namespace ClickAssistant.Core.Tests;

public class ProfileRescalerTests
{
    private static ScreenSnapshot SingleMonitor(int x, int y, int width, int height) => new()
    {
        Monitors = [new MonitorBounds(x, y, width, height, 1.0)]
    };

    private static ClickProfile ProfileWithPoint(ScreenPoint location) => new()
    {
        Points = [new ClickPoint { Location = location }]
    };

    [Fact]
    public void Rescale_ScalesPointProportionallyToNewResolution()
    {
        var from = SingleMonitor(0, 0, 1920, 1080);
        var to = SingleMonitor(0, 0, 3840, 2160); // 2x v obou osách
        var profile = ProfileWithPoint(new ScreenPoint(960, 540)); // přesný střed

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        Assert.Equal(new ScreenPoint(1920, 1080), rescaled.Points[0].Location);
    }

    [Fact]
    public void Rescale_UpdatesCapturedScreenSnapshotToNewOne()
    {
        var from = SingleMonitor(0, 0, 1920, 1080);
        var to = SingleMonitor(0, 0, 2560, 1440);
        var profile = ProfileWithPoint(new ScreenPoint(0, 0));

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        Assert.Same(to, rescaled.CapturedScreenSnapshot);
    }

    [Fact]
    public void Rescale_WithEmptySourceMonitors_ReturnsProfileUnchanged()
    {
        var from = new ScreenSnapshot { Monitors = [] };
        var to = SingleMonitor(0, 0, 1920, 1080);
        var profile = ProfileWithPoint(new ScreenPoint(100, 100));

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        Assert.Same(profile, rescaled);
    }

    [Fact]
    public void Rescale_WithEmptyTargetMonitors_ReturnsProfileUnchanged()
    {
        var from = SingleMonitor(0, 0, 1920, 1080);
        var to = new ScreenSnapshot { Monitors = [] };
        var profile = ProfileWithPoint(new ScreenPoint(100, 100));

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        Assert.Same(profile, rescaled);
    }

    [Fact]
    public void Rescale_PointOutsideAnyMonitor_FallsBackToFirstMonitorForMapping()
    {
        var from = SingleMonitor(0, 0, 1920, 1080);
        var to = SingleMonitor(0, 0, 960, 540); // 0.5x
        // Bod mimo hranice monitoru (např. starý záznam po změně sestavy monitorů).
        var profile = ProfileWithPoint(new ScreenPoint(5000, 5000));

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        // Poměr k monitoru 0 se přesto spočítá (i když bod leží mimo jeho hranice) - nesmí spadnout.
        var expectedRelX = 5000.0 / 1920;
        var expectedRelY = 5000.0 / 1080;
        var expected = new ScreenPoint(
            (int)Math.Round(expectedRelX * 960),
            (int)Math.Round(expectedRelY * 540));
        Assert.Equal(expected, rescaled.Points[0].Location);
    }

    [Fact]
    public void Rescale_SecondMonitor_MapsToMatchingIndexOnTarget()
    {
        var from = new ScreenSnapshot
        {
            Monitors =
            [
                new MonitorBounds(0, 0, 1920, 1080, 1.0),
                new MonitorBounds(1920, 0, 1280, 720, 1.0)
            ]
        };
        var to = new ScreenSnapshot
        {
            Monitors =
            [
                new MonitorBounds(0, 0, 1920, 1080, 1.0),
                new MonitorBounds(1920, 0, 2560, 1440, 1.0) // druhý monitor zdvojnásoben
            ]
        };
        var profile = ProfileWithPoint(new ScreenPoint(1920 + 640, 360)); // střed druhého monitoru

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        Assert.Equal(new ScreenPoint(1920 + 1280, 720), rescaled.Points[0].Location);
    }

    [Fact]
    public void Rescale_FewerTargetMonitorsThanSource_FallsBackToFirstTargetMonitor()
    {
        var from = new ScreenSnapshot
        {
            Monitors =
            [
                new MonitorBounds(0, 0, 1920, 1080, 1.0),
                new MonitorBounds(1920, 0, 1920, 1080, 1.0)
            ]
        };
        var to = SingleMonitor(0, 0, 1920, 1080); // druhý monitor zmizel
        var profile = ProfileWithPoint(new ScreenPoint(1920 + 960, 540)); // střed druhého monitoru

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        // monitorIndex=1 >= to.Monitors.Count(1) => spadne zpátky na to.Monitors[0]
        Assert.Equal(new ScreenPoint(960, 540), rescaled.Points[0].Location);
    }

    [Fact]
    public void Rescale_MultiplePoints_RescalesEachIndependently()
    {
        var from = SingleMonitor(0, 0, 1000, 1000);
        var to = SingleMonitor(0, 0, 2000, 2000);
        var profile = new ClickProfile
        {
            Points =
            [
                new ClickPoint { Name = "A", Location = new ScreenPoint(0, 0) },
                new ClickPoint { Name = "B", Location = new ScreenPoint(500, 500) },
                new ClickPoint { Name = "C", Location = new ScreenPoint(1000, 1000) }
            ]
        };

        var rescaled = ProfileRescaler.Rescale(profile, from, to);

        Assert.Equal(new ScreenPoint(0, 0), rescaled.Points[0].Location);
        Assert.Equal(new ScreenPoint(1000, 1000), rescaled.Points[1].Location);
        Assert.Equal(new ScreenPoint(2000, 2000), rescaled.Points[2].Location);
    }

    [Fact]
    public void Rescale_DoesNotMutateOriginalProfile()
    {
        var from = SingleMonitor(0, 0, 1000, 1000);
        var to = SingleMonitor(0, 0, 2000, 2000);
        var original = ProfileWithPoint(new ScreenPoint(500, 500));

        ProfileRescaler.Rescale(original, from, to);

        Assert.Equal(new ScreenPoint(500, 500), original.Points[0].Location);
        Assert.Null(original.CapturedScreenSnapshot);
    }
}
