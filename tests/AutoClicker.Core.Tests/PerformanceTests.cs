using System.Diagnostics;
using AutoClicker.Core.Engine;
using AutoClicker.Core.Engine.Movement;
using AutoClicker.Core.Engine.PointOrderStrategies;
using AutoClicker.Core.Models;
using AutoClicker.Core.Screen;
using AutoClicker.Infrastructure.Persistence;
using NSubstitute;
using Xunit;

namespace AutoClicker.Core.Tests;

/// <summary>
/// Hrubé výkonnostní testy hlavních "za běhu" cest enginu. Nejde o mikrobenchmarky (repo nemá závislost na
/// BenchmarkDotNet) - cílem je zachytit hrubé regrese (omylem přidaná synchronní práce v horké smyčce,
/// O(n^2) místo O(n), zbytečná alokace navíc), ne měřit přesné nanosekundy. Meze jsou proto záměrně
/// velkorysé (řádový strop, ne těsný), aby test neblikal na pomalejším/vytíženém CI stroji.
/// </summary>
public class PerformanceTests
{
    [Fact]
    public void BezierMovementPathGenerator_GeneratesManyPaths_WithinTimeBudget()
    {
        var generator = new BezierMovementPathGenerator();
        var config = new HumanizationConfig { Enabled = true, UseCurvedMovement = true };
        var rng = new Random(1);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 5000; i++)
        {
            var start = new ScreenPoint(rng.Next(0, 3840), rng.Next(0, 2160));
            var end = new ScreenPoint(rng.Next(0, 3840), rng.Next(0, 2160));
            generator.GeneratePath(start, end, config, rng);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"5000 generovaných trajektorií trvalo {sw.ElapsedMilliseconds} ms, čekalo se < 2000 ms.");
    }

    [Fact]
    public void RandomOrderStrategy_ShufflesLargePointList_WithinTimeBudget()
    {
        var points = Enumerable.Range(0, 50_000).Select(_ => new ClickPoint()).ToList();
        var strategy = new RandomOrderStrategy();
        var rng = new Random(1);

        var sw = Stopwatch.StartNew();
        var order = strategy.GetOrder(points, null, rng).ToList();
        sw.Stop();

        Assert.Equal(points.Count, order.Count);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Shuffle 50 000 bodů trval {sw.ElapsedMilliseconds} ms, čekalo se < 500 ms.");
    }

    [Fact]
    public void ProfileRescaler_RescalesLargeProfile_WithinTimeBudget()
    {
        var rng = new Random(1);
        var points = Enumerable.Range(0, 5000)
            .Select(_ => new ClickPoint { Location = new ScreenPoint(rng.Next(0, 1920), rng.Next(0, 1080)) })
            .ToList();
        var profile = new ClickProfile { Points = points };
        var from = new ScreenSnapshot { Monitors = [new MonitorBounds(0, 0, 1920, 1080, 1.0)] };
        var to = new ScreenSnapshot { Monitors = [new MonitorBounds(0, 0, 2560, 1440, 1.0)] };

        var sw = Stopwatch.StartNew();
        var rescaled = ProfileRescaler.Rescale(profile, from, to);
        sw.Stop();

        Assert.Equal(5000, rescaled.Points.Count);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"Přepočet 5000 bodů trval {sw.ElapsedMilliseconds} ms, čekalo se < 500 ms.");
    }

    [Fact]
    public async Task JsonProfileRepository_RoundTripsLargeProfile_WithinTimeBudget()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AutoClickerPerfTests_" + Guid.NewGuid());
        var repository = new JsonProfileRepository(tempDir);
        try
        {
            var points = Enumerable.Range(0, 5000)
                .Select(i => new ClickPoint { Name = $"Bod {i}", Location = new ScreenPoint(i % 1920, i % 1080) })
                .ToList();
            var profile = new ClickProfile { Name = "Velký profil", Points = points };

            var sw = Stopwatch.StartNew();
            await repository.SaveAsync(profile);
            var loaded = await repository.LoadAllAsync();
            sw.Stop();

            Assert.Equal(5000, Assert.Single(loaded).Points.Count);
            Assert.True(sw.ElapsedMilliseconds < 3000,
                $"Uložení a načtení 5000bodového profilu trvalo {sw.ElapsedMilliseconds} ms, čekalo se < 3000 ms.");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ClickSequenceExecutor_LoopOverheadPerPoint_StaysCloseToIntendedDelayFloor()
    {
        // ExecuteSinglePointAsync má natvrdo 50ms delay mezi MouseDown/MouseUp (viz technicalExplanation.md B.5.3)
        // a TimingJitter má tvrdé minimum 10ms - u 30 bodů s BaseIntervalMs=0/JitterMs=0 je teoretické minimum
        // běhu 30 * (50 + 10) = 1800 ms. Test hlídá, že smyčka k tomuto minimu nepřidává znatelnou režii navíc
        // (např. omylem vloženou synchronní práci do horké cesty typu screen capture nebo JSON serializaci).
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        const int pointCount = 30;
        var profile = new ClickProfile
        {
            Points = Enumerable.Range(0, pointCount)
                .Select(i => new ClickPoint { Location = new ScreenPoint(i, i) })
                .ToList(),
            Timing = new TimingConfig { BaseIntervalMs = 0, JitterMs = 0, Repeat = RepeatMode.Once }
        };

        var stopped = new TaskCompletionSource();
        executor.StatusChanged += (_, e) =>
        {
            if (e.Status == EngineStatus.Stopped) stopped.TrySetResult();
        };

        var sw = Stopwatch.StartNew();
        await executor.StartAsync(profile);
        var completed = await Task.WhenAny(stopped.Task, Task.Delay(10_000));
        sw.Stop();

        Assert.Same(stopped.Task, completed);
        const double theoreticalFloorMs = pointCount * (50 + 10);
        Assert.True(sw.ElapsedMilliseconds < theoreticalFloorMs * 3,
            $"{pointCount} bodů trvalo {sw.ElapsedMilliseconds} ms, teoretické minimum je {theoreticalFloorMs} ms " +
            "- smyčka přidává neúměrnou režii navíc k zamýšleným delayům.");
    }
}
