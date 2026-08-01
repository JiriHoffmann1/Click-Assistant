using AutoClicker.Core.Engine.PointOrderStrategies;
using AutoClicker.Core.Models;

namespace AutoClicker.Core.Engine;

/// <summary>
/// Fáze 1: rovný pohyb kurzoru bez humanizace, jen sekvenční pořadí. Rozšiřuje se
/// v dalších fázích (jitter, Bézierův pohyb, Random/CustomOrder).
/// </summary>
public sealed class ClickSequenceExecutor
{
    private readonly IInputSimulator _simulator;
    private CancellationTokenSource? _cts;

    public event EventHandler<EngineStatusEventArgs>? StatusChanged;
    public event EventHandler<ClickPoint>? PointClicked;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public ClickSequenceExecutor(IInputSimulator simulator)
    {
        _simulator = simulator;
    }

    public Task StartAsync(ClickProfile profile)
    {
        if (IsRunning || profile.Points.Count == 0) return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => RunLoopAsync(profile, token), token);
        return Task.CompletedTask;
    }

    public void Stop() => _cts?.Cancel();

    private async Task RunLoopAsync(ClickProfile profile, CancellationToken token)
    {
        var orderStrategy = new SequentialOrderStrategy();
        var rng = new Random();
        int cyclesRun = 0;

        RaiseStatus(EngineStatus.Running);
        try
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var point in orderStrategy.GetOrder(profile.Points, profile.CustomOrder, rng))
                {
                    token.ThrowIfCancellationRequested();
                    await ExecuteSinglePointAsync(point, token);
                    PointClicked?.Invoke(this, point);

                    var delay = point.DelayAfterMsOverride ?? profile.Timing.BaseIntervalMs;
                    await CancellableDelay(delay, token);
                }

                cyclesRun++;
                if (profile.Timing.Repeat == RepeatMode.Once) break;
                if (profile.Timing.Repeat == RepeatMode.FixedCount && cyclesRun >= profile.Timing.RepeatCount) break;
            }
        }
        catch (OperationCanceledException)
        {
            // očekávané při Stop()
        }
        finally
        {
            RaiseStatus(EngineStatus.Stopped);
        }
    }

    private async Task ExecuteSinglePointAsync(ClickPoint point, CancellationToken token)
    {
        _simulator.MoveMouse(point.Location);

        for (int i = 0; i < point.ClickCount; i++)
        {
            token.ThrowIfCancellationRequested();
            _simulator.MouseDown(point.Button);
            await CancellableDelay(50, token);
            _simulator.MouseUp(point.Button);
            if (point.ClickCount > 1) await CancellableDelay(80, token);
        }
    }

    private static Task CancellableDelay(int ms, CancellationToken token) =>
        ms <= 0 ? Task.CompletedTask : Task.Delay(ms, token);

    private void RaiseStatus(EngineStatus status) =>
        StatusChanged?.Invoke(this, new EngineStatusEventArgs(status));
}
