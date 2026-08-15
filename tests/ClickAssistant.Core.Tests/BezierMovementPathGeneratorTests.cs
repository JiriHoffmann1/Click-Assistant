using ClickAssistant.Core.Engine.Movement;
using ClickAssistant.Core.Models;
using Xunit;

namespace ClickAssistant.Core.Tests;

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

    [Fact]
    public void GeneratePath_WithGuaranteedOvershoot_StillEndsExactlyAtTarget()
    {
        var generator = new BezierMovementPathGenerator();
        var start = new ScreenPoint(0, 0);
        var end = new ScreenPoint(500, 300);

        // overshootChance = 1 => vždy se přidá korekční "přestřelení" na konec dráhy.
        var path = generator.GeneratePath(start, end, DefaultConfig(overshootChance: 1), new Random(3));

        Assert.Equal(end, path[^1].Point);
        // Přestřelení přidává dva extra kroky (overshoot bod + návrat na cíl) nad běžný počet kroků.
        var withoutOvershoot = generator.GeneratePath(start, end, DefaultConfig(overshootChance: 0), new Random(3));
        Assert.Equal(withoutOvershoot.Count + 2, path.Count);
    }

    [Fact]
    public void GeneratePath_OvershootPoint_IsNotEqualToFinalTarget()
    {
        var generator = new BezierMovementPathGenerator();
        var start = new ScreenPoint(0, 0);
        var end = new ScreenPoint(500, 300);

        var path = generator.GeneratePath(start, end, DefaultConfig(overshootChance: 1), new Random(3));

        // Předposlední bod je "přestřelení" - musí se lišit od finálního cíle, jinak by korekce byla neviditelná.
        var overshootPoint = path[^2].Point;
        Assert.NotEqual(end, overshootPoint);
    }

    [Fact]
    public void GeneratePath_WithInvertedDurationRange_DoesNotThrow()
    {
        // Obranná úprava: MovementDurationMsMin > MovementDurationMsMax může nastat u ručně
        // upraveného/poškozeného JSON profilu na disku. Random.Next(min, max) by jinak vyhodil
        // ArgumentOutOfRangeException a shodil běžící klikací smyčku.
        var generator = new BezierMovementPathGenerator();
        var config = new HumanizationConfig
        {
            Enabled = true,
            UseCurvedMovement = true,
            MovementDurationMsMin = 500,
            MovementDurationMsMax = 100,
            CurveBowStrength = 0.25,
            OvershootChance = 0
        };

        var exception = Record.Exception(() =>
            generator.GeneratePath(new ScreenPoint(0, 0), new ScreenPoint(300, 300), config, new Random(1)));

        Assert.Null(exception);
    }

    [Fact]
    public void GeneratePath_WithNegativeDurationBounds_DoesNotThrow()
    {
        var generator = new BezierMovementPathGenerator();
        var config = new HumanizationConfig
        {
            Enabled = true,
            UseCurvedMovement = true,
            MovementDurationMsMin = -100,
            MovementDurationMsMax = -50,
            CurveBowStrength = 0.25,
            OvershootChance = 0
        };

        var exception = Record.Exception(() =>
            generator.GeneratePath(new ScreenPoint(0, 0), new ScreenPoint(300, 300), config, new Random(1)));

        Assert.Null(exception);
    }

    [Fact]
    public void GeneratePath_IsDeterministic_ForSameSeed()
    {
        var generator = new BezierMovementPathGenerator();
        var start = new ScreenPoint(10, 10);
        var end = new ScreenPoint(400, 250);

        var path1 = generator.GeneratePath(start, end, DefaultConfig(), new Random(99));
        var path2 = generator.GeneratePath(start, end, DefaultConfig(), new Random(99));

        Assert.Equal(path1, path2);
    }
}
