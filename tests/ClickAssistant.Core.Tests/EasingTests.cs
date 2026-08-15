using ClickAssistant.Core.Engine.Movement;
using Xunit;

namespace ClickAssistant.Core.Tests;

public class EasingTests
{
    [Fact]
    public void EaseInOutCubic_AtZero_ReturnsZero()
    {
        Assert.Equal(0, Easing.EaseInOutCubic(0), precision: 10);
    }

    [Fact]
    public void EaseInOutCubic_AtOne_ReturnsOne()
    {
        Assert.Equal(1, Easing.EaseInOutCubic(1), precision: 10);
    }

    [Fact]
    public void EaseInOutCubic_AtMidpoint_ReturnsOneHalf()
    {
        Assert.Equal(0.5, Easing.EaseInOutCubic(0.5), precision: 10);
    }

    [Theory]
    [InlineData(0.0, 0.1)]
    [InlineData(0.1, 0.2)]
    [InlineData(0.2, 0.3)]
    [InlineData(0.3, 0.4)]
    [InlineData(0.4, 0.5)]
    [InlineData(0.5, 0.6)]
    [InlineData(0.6, 0.7)]
    [InlineData(0.7, 0.8)]
    [InlineData(0.8, 0.9)]
    [InlineData(0.9, 1.0)]
    public void EaseInOutCubic_IsMonotonicallyIncreasing(double earlier, double later)
    {
        Assert.True(Easing.EaseInOutCubic(later) > Easing.EaseInOutCubic(earlier));
    }

    [Fact]
    public void EaseInOutCubic_StaysWithinZeroToOneRange()
    {
        for (double t = 0; t <= 1.0; t += 0.01)
        {
            var eased = Easing.EaseInOutCubic(t);
            Assert.InRange(eased, -0.0001, 1.0001);
        }
    }
}
