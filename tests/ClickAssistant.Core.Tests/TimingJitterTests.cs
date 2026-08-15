using ClickAssistant.Core.Engine.Jitter;
using Xunit;

namespace ClickAssistant.Core.Tests;

public class TimingJitterTests
{
    [Fact]
    public void Compute_WithZeroJitter_ReturnsBaseValue()
    {
        var result = TimingJitter.Compute(500, 0, new Random());
        Assert.Equal(500, result);
    }

    [Fact]
    public void Compute_StaysWithinConfiguredRange()
    {
        var rng = new Random(7);
        for (int i = 0; i < 500; i++)
        {
            var result = TimingJitter.Compute(500, 50, rng);
            Assert.InRange(result, 450, 550);
        }
    }

    [Fact]
    public void Compute_NeverGoesBelowMinimumFloor()
    {
        var rng = new Random(3);
        for (int i = 0; i < 500; i++)
        {
            var result = TimingJitter.Compute(15, 100, rng);
            Assert.True(result >= 10);
        }
    }

    [Fact]
    public void Compute_WithNegativeBaseAndZeroJitter_FloorsToMinimum()
    {
        var result = TimingJitter.Compute(-500, 0, new Random());
        Assert.Equal(10, result);
    }

    [Fact]
    public void Compute_WithNegativeJitter_TreatedAsZero()
    {
        var result = TimingJitter.Compute(500, -50, new Random());
        Assert.Equal(500, result);
    }

    [Fact]
    public void Compute_WithZeroBaseAndLargeJitter_NeverGoesNegativeOrBelowFloor()
    {
        var rng = new Random(9);
        for (int i = 0; i < 500; i++)
        {
            var result = TimingJitter.Compute(0, 1000, rng);
            Assert.True(result >= 10);
        }
    }
}
