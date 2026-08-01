using AutoClicker.Core.Engine.Movement;
using AutoClicker.Core.Models;
using Xunit;

namespace AutoClicker.Core.Tests;

public class BezierMovementPathGeneratorTests
{
    private static HumanizationConfig DefaultConfig(double overshootChance = 0) => new()
    {
        Enabled = true,
        UseCurvedMovement = true,
        MovementDurationMsMin = 100,
        MovementDurationMsMax = 200,
        CurveBowStrength = 0.25,
        OvershootChance = overshootChance
    };

    [Fact]
    public void GeneratePath_EndsAtTargetPoint_WithoutOvershoot()
    {
        var generator = new BezierMovementPathGenerator();
        var start = new ScreenPoint(0, 0);
        var end = new ScreenPoint(500, 300);

        var path = generator.GeneratePath(start, end, DefaultConfig(overshootChance: 0), new Random(1));

        Assert.Equal(end, path[^1].Point);
    }

    [Fact]
    public void GeneratePath_StepCountIsWithinExpectedBounds()
    {
        var generator = new BezierMovementPathGenerator();

        var shortPath = generator.GeneratePath(new ScreenPoint(0, 0), new ScreenPoint(10, 0), DefaultConfig(), new Random(2));
        var longPath = generator.GeneratePath(new ScreenPoint(0, 0), new ScreenPoint(2000, 0), DefaultConfig(), new Random(2));

        Assert.InRange(shortPath.Count, 1, 48);
        Assert.InRange(longPath.Count, 12, 48);
    }

    [Fact]
    public void GeneratePath_AllStepDelaysAreNonNegative()
    {
        var generator = new BezierMovementPathGenerator();
        var path = generator.GeneratePath(new ScreenPoint(0, 0), new ScreenPoint(800, 600), DefaultConfig(), new Random(5));

        Assert.All(path, step => Assert.True(step.StepDelayMs >= 0));
    }

    [Fact]
    public void GeneratePath_SamePointStartAndEnd_ReturnsSingleStep()
    {
        var generator = new BezierMovementPathGenerator();
        var point = new ScreenPoint(100, 100);

        var path = generator.GeneratePath(point, point, DefaultConfig(), new Random());

        var single = Assert.Single(path);
        Assert.Equal(point, single.Point);
    }
}
