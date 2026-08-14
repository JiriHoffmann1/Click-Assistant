using AutoClicker.Core.Models;
using Xunit;

namespace AutoClicker.Core.Tests;

public class ScreenSnapshotTests
{
    private static MonitorBounds Primary => new(0, 0, 1920, 1080, 1.0);

    [Fact]
    public void IsCompatibleWith_IdenticalSingleMonitor_ReturnsTrue()
    {
        var a = new ScreenSnapshot { Monitors = [Primary] };
        var b = new ScreenSnapshot { Monitors = [Primary] };

        Assert.True(a.IsCompatibleWith(b));
    }

    [Fact]
    public void IsCompatibleWith_DifferentMonitorCount_ReturnsFalse()
    {
        var a = new ScreenSnapshot { Monitors = [Primary] };
        var b = new ScreenSnapshot { Monitors = [Primary, Primary] };

        Assert.False(a.IsCompatibleWith(b));
    }

    [Fact]
    public void IsCompatibleWith_DifferentResolution_ReturnsFalse()
    {
        var a = new ScreenSnapshot { Monitors = [Primary] };
        var b = new ScreenSnapshot { Monitors = [new MonitorBounds(0, 0, 2560, 1440, 1.0)] };

        Assert.False(a.IsCompatibleWith(b));
    }

    [Fact]
    public void IsCompatibleWith_DifferentPosition_ReturnsFalse()
    {
        var a = new ScreenSnapshot { Monitors = [Primary] };
        var b = new ScreenSnapshot { Monitors = [new MonitorBounds(100, 0, 1920, 1080, 1.0)] };

        Assert.False(a.IsCompatibleWith(b));
    }

    [Fact]
    public void IsCompatibleWith_DifferentScalingOnly_StillReturnsTrue()
    {
        // Scaling (DPI) se do porovnání záměrně nepočítá - jen X/Y/Width/Height.
        var a = new ScreenSnapshot { Monitors = [Primary] };
        var b = new ScreenSnapshot { Monitors = [new MonitorBounds(0, 0, 1920, 1080, 1.25)] };

        Assert.True(a.IsCompatibleWith(b));
    }

    [Fact]
    public void IsCompatibleWith_BothEmpty_ReturnsTrue()
    {
        var a = new ScreenSnapshot { Monitors = [] };
        var b = new ScreenSnapshot { Monitors = [] };

        Assert.True(a.IsCompatibleWith(b));
    }

    [Fact]
    public void IsCompatibleWith_SameCountDifferentOrder_ReturnsFalse()
    {
        var second = new MonitorBounds(1920, 0, 1280, 720, 1.0);
        var a = new ScreenSnapshot { Monitors = [Primary, second] };
        var b = new ScreenSnapshot { Monitors = [second, Primary] };

        Assert.False(a.IsCompatibleWith(b));
    }
}
