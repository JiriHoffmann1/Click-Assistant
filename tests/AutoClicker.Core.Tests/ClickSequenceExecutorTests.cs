using AutoClicker.Core.Engine;
using AutoClicker.Core.Models;
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

    private static async Task RunToCompletionAsync(ClickSequenceExecutor executor, ClickProfile profile)
    {
        var stopped = new TaskCompletionSource();
        executor.StatusChanged += (_, e) =>
        {
            if (e.Status == EngineStatus.Stopped) stopped.TrySetResult();
        };

        await executor.StartAsync(profile);
        var completed = await Task.WhenAny(stopped.Task, Task.Delay(5000));
        Assert.Same(stopped.Task, completed);
    }
}
