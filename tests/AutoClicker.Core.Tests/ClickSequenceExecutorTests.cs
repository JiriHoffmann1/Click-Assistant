using AutoClicker.Core.Engine;
using AutoClicker.Core.Engine.Movement;
using AutoClicker.Core.Models;
using AutoClicker.Core.Screen;
using NSubstitute;
using Xunit;

namespace AutoClicker.Core.Tests;

public class ClickSequenceExecutorTests
{
    private static ClickProfile SingleFastPointProfile(RepeatMode repeat = RepeatMode.FixedCount, int repeatCount = 3) => new()
    {
        Points = new List<ClickPoint> { new() { Location = new ScreenPoint(10, 20) } },
        Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = repeat, RepeatCount = repeatCount }
    };

    [Fact]
    public async Task StartAsync_ClicksEachPointInOrder()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var profile = new ClickProfile
        {
            Points = new List<ClickPoint>
            {
                new() { Name = "A", Location = new ScreenPoint(1, 1) },
                new() { Name = "B", Location = new ScreenPoint(2, 2) }
            },
            Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = RepeatMode.Once }
        };

        await RunToCompletionAsync(executor, profile);

        Received.InOrder(() =>
        {
            simulator.MoveMouse(new ScreenPoint(1, 1));
            simulator.MouseDown(MouseButtonType.Left);
            simulator.MouseUp(MouseButtonType.Left);
            simulator.MoveMouse(new ScreenPoint(2, 2));
            simulator.MouseDown(MouseButtonType.Left);
            simulator.MouseUp(MouseButtonType.Left);
        });
    }

    [Fact]
    public async Task StartAsync_RespectsFixedRepeatCount()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var clickedCount = 0;
        executor.PointClicked += (_, _) => clickedCount++;

        await RunToCompletionAsync(executor, SingleFastPointProfile(RepeatMode.FixedCount, 3));

        Assert.Equal(3, clickedCount);
    }

    [Fact]
    public async Task Stop_CancelsInfiniteLoopPromptly()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var profile = SingleFastPointProfile(RepeatMode.Infinite);

        var stopped = new TaskCompletionSource();
        executor.StatusChanged += (_, e) =>
        {
            if (e.Status == EngineStatus.Stopped) stopped.TrySetResult();
        };

        await executor.StartAsync(profile);
        await Task.Delay(20); // ať proběhne pár cyklů
        executor.Stop();

        var completed = await Task.WhenAny(stopped.Task, Task.Delay(2000));
        Assert.Same(stopped.Task, completed);
        Assert.False(executor.IsRunning);
    }

    [Fact]
    public async Task StartAsync_WithNoPoints_DoesNothing()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var profile = new ClickProfile { Points = new List<ClickPoint>() };

        await executor.StartAsync(profile);

        Assert.False(executor.IsRunning);
        simulator.DidNotReceive().MoveMouse(Arg.Any<ScreenPoint>());
    }

    [Fact]
    public async Task StartAsync_WhileAlreadyRunning_IgnoresSecondProfile()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var runningProfile = SingleFastPointProfile(RepeatMode.Infinite);
        var otherProfile = new ClickProfile
        {
            Points = new List<ClickPoint> { new() { Location = new ScreenPoint(999, 999) } },
            Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = RepeatMode.Infinite }
        };

        await executor.StartAsync(runningProfile);
        await Task.Delay(20);
        await executor.StartAsync(otherProfile); // musí být no-op, executor už běží

        executor.Stop();
        await Task.Delay(50);

        simulator.DidNotReceive().MoveMouse(new ScreenPoint(999, 999));
    }

    [Fact]
    public async Task StartAsync_ClickCountGreaterThanOne_PressesMouseThatManyTimes()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var profile = new ClickProfile
        {
            Points = new List<ClickPoint> { new() { Location = new ScreenPoint(5, 5), ClickCount = 3 } },
            Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = RepeatMode.Once }
        };

        await RunToCompletionAsync(executor, profile);

        simulator.Received(3).MouseDown(MouseButtonType.Left);
        simulator.Received(3).MouseUp(MouseButtonType.Left);
    }

    [Fact]
    public async Task StartAsync_UsesPerPointDelayOverride_InsteadOfGlobalInterval()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var profile = new ClickProfile
        {
            Points = new List<ClickPoint> { new() { Location = new ScreenPoint(1, 1), DelayAfterMsOverride = 1 } },
            // Globální interval je záměrně obrovský - test musí doběhnout rychle, jinak override nefunguje.
            Timing = new TimingConfig { BaseIntervalMs = 10_000, Repeat = RepeatMode.Once }
        };

        var stopped = new TaskCompletionSource();
        executor.StatusChanged += (_, e) =>
        {
            if (e.Status == EngineStatus.Stopped) stopped.TrySetResult();
        };

        await executor.StartAsync(profile);
        var completed = await Task.WhenAny(stopped.Task, Task.Delay(1500));

        Assert.Same(stopped.Task, completed);
    }

    [Fact]
    public async Task StartAsync_CustomOrder_ClicksPointsInSpecifiedIdOrder()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var pointA = new ClickPoint { Name = "A", Location = new ScreenPoint(1, 1) };
        var pointB = new ClickPoint { Name = "B", Location = new ScreenPoint(2, 2) };
        var profile = new ClickProfile
        {
            Points = new List<ClickPoint> { pointA, pointB },
            OrderMode = SequenceOrderMode.CustomOrder,
            CustomOrder = new List<Guid> { pointB.Id, pointA.Id },
            Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = RepeatMode.Once }
        };

        await RunToCompletionAsync(executor, profile);

        Received.InOrder(() =>
        {
            simulator.MoveMouse(new ScreenPoint(2, 2));
            simulator.MoveMouse(new ScreenPoint(1, 1));
        });
    }

    [Fact]
    public async Task StartAsync_CustomOrderWithNoMatchingIds_CompletesWithoutHangingOrCrashing()
    {
        // Vlastní pořadí odkazuje na neexistující body (např. smazané po nastavení pořadí) -
        // CustomOrderStrategy vrátí prázdný seznam. Smyčka nesmí zamrznout ani spadnout.
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var profile = new ClickProfile
        {
            Points = new List<ClickPoint> { new() { Location = new ScreenPoint(1, 1) } },
            OrderMode = SequenceOrderMode.CustomOrder,
            CustomOrder = new List<Guid> { Guid.NewGuid() },
            Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = RepeatMode.FixedCount, RepeatCount = 2 }
        };
        var clicked = false;
        executor.PointClicked += (_, _) => clicked = true;

        await RunToCompletionAsync(executor, profile);

        Assert.False(clicked);
        simulator.DidNotReceiveWithAnyArgs().MoveMouse(default);
    }

    [Fact]
    public async Task RandomNoImmediateRepeat_NeverClicksSamePointTwiceInARowAcrossCycles()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var executor = new ClickSequenceExecutor(simulator);
        var pointA = new ClickPoint { Name = "A", Location = new ScreenPoint(1, 1) };
        var pointB = new ClickPoint { Name = "B", Location = new ScreenPoint(2, 2) };
        var profile = new ClickProfile
        {
            Points = new List<ClickPoint> { pointA, pointB },
            OrderMode = SequenceOrderMode.RandomNoImmediateRepeat,
            Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = RepeatMode.FixedCount, RepeatCount = 20 }
        };

        var clickedIds = new List<Guid>();
        executor.PointClicked += (_, point) => clickedIds.Add(point.Id);

        await RunToCompletionAsync(executor, profile, timeoutMs: 10_000);

        for (int i = 1; i < clickedIds.Count; i++)
        {
            Assert.NotEqual(clickedIds[i - 1], clickedIds[i]);
        }
    }

    [Fact]
    public async Task RunLoop_DetectsResolutionChangeDuringRun_RaisesEventAndStopsAutomatically()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var screenInfo = Substitute.For<IScreenInfoProvider>();
        var originalSnapshot = new ScreenSnapshot { Monitors = [new MonitorBounds(0, 0, 1920, 1080, 1.0)] };
        var changedSnapshot = new ScreenSnapshot { Monitors = [new MonitorBounds(0, 0, 2560, 1440, 1.0)] };
        screenInfo.GetCurrentSnapshot().Returns(changedSnapshot);

        var executor = new ClickSequenceExecutor(simulator, screenInfoProvider: screenInfo);
        var profile = SingleFastPointProfile(RepeatMode.Infinite) with { CapturedScreenSnapshot = originalSnapshot };

        ResolutionChangedEventArgs? raisedArgs = null;
        var stopped = new TaskCompletionSource();
        executor.ResolutionChangedDuringRun += (_, e) => raisedArgs = e;
        executor.StatusChanged += (_, e) =>
        {
            if (e.Status == EngineStatus.Stopped) stopped.TrySetResult();
        };

        await executor.StartAsync(profile);
        var completed = await Task.WhenAny(stopped.Task, Task.Delay(2000));

        Assert.Same(stopped.Task, completed);
        Assert.NotNull(raisedArgs);
        Assert.Same(originalSnapshot, raisedArgs!.Previous);
        Assert.Same(changedSnapshot, raisedArgs.Current);
    }

    [Fact]
    public async Task RunLoop_NoResolutionChange_KeepsRunningUntilExplicitStop()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var screenInfo = Substitute.For<IScreenInfoProvider>();
        var sameSnapshot = new ScreenSnapshot { Monitors = [new MonitorBounds(0, 0, 1920, 1080, 1.0)] };
        screenInfo.GetCurrentSnapshot().Returns(sameSnapshot);

        var executor = new ClickSequenceExecutor(simulator, screenInfoProvider: screenInfo);
        var profile = SingleFastPointProfile(RepeatMode.Infinite) with { CapturedScreenSnapshot = sameSnapshot };

        var resolutionChangedRaised = false;
        executor.ResolutionChangedDuringRun += (_, _) => resolutionChangedRaised = true;

        await executor.StartAsync(profile);
        await Task.Delay(50);
        Assert.True(executor.IsRunning);
        Assert.False(resolutionChangedRaised);

        executor.Stop();
    }

    [Fact]
    public async Task StartAsync_HumanizedCurvedMovement_UsesInjectedPathGenerator()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var pathGenerator = Substitute.For<IMovementPathGenerator>();
        var fakePath = new List<(ScreenPoint Point, int StepDelayMs)>
        {
            (new ScreenPoint(50, 50), 0),
            (new ScreenPoint(100, 100), 0)
        };
        pathGenerator.GeneratePath(Arg.Any<ScreenPoint>(), Arg.Any<ScreenPoint>(), Arg.Any<HumanizationConfig>(), Arg.Any<Random>())
            .Returns(fakePath);

        var executor = new ClickSequenceExecutor(simulator, movementGenerator: pathGenerator);
        var profile = new ClickProfile
        {
            // Jediný bod: lastCursorPos je před smyčkou nastaven na Points[0].Location, takže
            // se křivkový pohyb (mockovaný generátor) uplatní přesně jednou.
            Points = new List<ClickPoint> { new() { Name = "Target", Location = new ScreenPoint(100, 100) } },
            Timing = new TimingConfig { BaseIntervalMs = 1, Repeat = RepeatMode.Once },
            Humanization = new HumanizationConfig { Enabled = true, UseCurvedMovement = true }
        };

        await RunToCompletionAsync(executor, profile);

        pathGenerator.Received(1).GeneratePath(Arg.Any<ScreenPoint>(), Arg.Any<ScreenPoint>(), Arg.Any<HumanizationConfig>(), Arg.Any<Random>());
        simulator.Received(1).MoveMouse(new ScreenPoint(50, 50));
        simulator.Received(1).MoveMouse(new ScreenPoint(100, 100));
    }

    private static Task RunToCompletionAsync(ClickSequenceExecutor executor, ClickProfile profile, int timeoutMs = 5000)
        => RunToCompletionCoreAsync(executor, profile, timeoutMs);

    private static async Task RunToCompletionCoreAsync(ClickSequenceExecutor executor, ClickProfile profile, int timeoutMs)
    {
        var stopped = new TaskCompletionSource();
        executor.StatusChanged += (_, e) =>
        {
            if (e.Status == EngineStatus.Stopped) stopped.TrySetResult();
        };

        await executor.StartAsync(profile);
        var completed = await Task.WhenAny(stopped.Task, Task.Delay(timeoutMs));
        Assert.Same(stopped.Task, completed);
    }
}
