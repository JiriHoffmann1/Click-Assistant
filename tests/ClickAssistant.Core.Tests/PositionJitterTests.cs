using ClickAssistant.Core.Engine.Jitter;
using ClickAssistant.Core.Models;
using Xunit;

namespace ClickAssistant.Core.Tests;

public class PositionJitterTests
{
    [Fact]
    public void Apply_WithZeroRadius_ReturnsExactCenter()
    {
        var center = new ScreenPoint(100, 100);
        var result = PositionJitter.Apply(center, 0, new Random());
        Assert.Equal(center, result);
    }

    [Fact]
    public void Apply_StaysWithinConfiguredRadius()
    {
        var center = new ScreenPoint(500, 500);
        var rng = new Random(11);

        for (int i = 0; i < 1000; i++)
        {
            var result = PositionJitter.Apply(center, 10, rng);
            double distance = Math.Sqrt(Math.Pow(result.X - center.X, 2) + Math.Pow(result.Y - center.Y, 2));
            Assert.True(distance <= 10.5, $"distance {distance} exceeded radius");
        }
    }

    [Fact]
    public void Apply_ProducesVariedOffsets_NotAlwaysSamePixel()
    {
        var center = new ScreenPoint(0, 0);
        var rng = new Random(21);

        var results = Enumerable.Range(0, 50).Select(_ => PositionJitter.Apply(center, 5, rng)).Distinct().ToList();

        Assert.True(results.Count > 5, "očekával jsem rozmanité posuny, ne pořád stejný bod");
    }

    [Fact]
    public void Apply_WithNegativeRadius_ReturnsExactCenter()
    {
        var center = new ScreenPoint(50, 50);
        var result = PositionJitter.Apply(center, -10, new Random());
        Assert.Equal(center, result);
    }

    [Fact]
    public void Apply_CanProduceNegativeCoordinates_NearScreenOrigin()
    {
        var center = new ScreenPoint(2, 2);
        var rng = new Random(4);

        var results = Enumerable.Range(0, 200).Select(_ => PositionJitter.Apply(center, 10, rng)).ToList();

        Assert.Contains(results, p => p.X < 0 || p.Y < 0);
    }
}
