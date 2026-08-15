using ClickAssistant.Core.Engine.Jitter;
using ClickAssistant.Core.Engine.Movement;
using ClickAssistant.Core.Engine.PointOrderStrategies;
using ClickAssistant.Core.Models;
using ClickAssistant.Core.Screen;

namespace ClickAssistant.Core.Engine;

/// <summary>
/// Klikací smyčka: podporuje více bodů, volitelné pořadí, jitter intervalu a volitelnou
/// humanizaci (jitter pozice kliku + Bézierův pohyb kurzoru mezi body).
/// </summary>
public sealed class ClickSequenceExecutor
{
    private readonly IInputSimulator _simulator;
    private readonly IMovementPathGenerator _movementGenerator;
    private readonly IScreenInfoProvider? _screenInfoProvider;
    private CancellationTokenSource? _cts;

    public event EventHandler<EngineStatusEventArgs>? StatusChanged;
    public event EventHandler<ClickPoint>? PointClicked;
    public event EventHandler<ResolutionChangedEventArgs>? ResolutionChangedDuringRun;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public ClickSequenceExecutor(
        IInputSimulator simulator,
        IMovementPathGenerator? movementGenerator = null,
        IScreenInfoProvider? screenInfoProvider = null)
    {
        _simulator = simulator;
        _movementGenerator = movementGenerator ?? new BezierMovementPathGenerator();
        _screenInfoProvider = screenInfoProvider;
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
        var orderStrategy = PointOrderStrategyFactory.Create(profile.OrderMode);
        var rng = new Random();
        int cyclesRun = 0;
        Guid? lastPointId = null;
        ScreenPoint? lastCursorPos = profile.Points.Count > 0 ? profile.Points[0].Location : null;

        RaiseStatus(EngineStatus.Running);
        try
        {
            while (!token.IsCancellationRequested)
            {
                var order = orderStrategy.GetOrder(profile.Points, profile.CustomOrder, rng).ToList();

                // U "bez okamžitého opakování" prohodit první bod, pokud navazuje na poslední bod minulého cyklu.
                if (profile.OrderMode == SequenceOrderMode.RandomNoImmediateRepeat && order.Count > 1
                    && lastPointId.HasValue && order[0].Id == lastPointId)
                {
                    (order[0], order[1]) = (order[1], order[0]);
                }

                if (order.Count == 0)
                {
                    // Vlastní pořadí odkazuje jen na neexistující/smazané body - není co kliknout.
                    // Bez tohoto čekání by nekonečné opakování bez těchto delay pointů vytížilo CPU na 100 %.
                    await CancellableDelay(100, token);
                }

                foreach (var point in order)
                {
                    token.ThrowIfCancellationRequested();
                    lastCursorPos = await ExecuteSinglePointAsync(point, profile.Humanization, lastCursorPos, rng, token);
                    PointClicked?.Invoke(this, point);
                    lastPointId = point.Id;

                    var baseDelay = point.DelayAfterMsOverride ?? profile.Timing.BaseIntervalMs;
                    var delay = TimingJitter.Compute(baseDelay, profile.Timing.JitterMs, rng);
                    await CancellableDelay(delay, token);
                }

                cyclesRun++;
                if (profile.Timing.Repeat == RepeatMode.Once) break;
                if (profile.Timing.Repeat == RepeatMode.FixedCount && cyclesRun >= profile.Timing.RepeatCount) break;

                if (CheckResolutionChanged(profile)) break;
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

    private bool CheckResolutionChanged(ClickProfile profile)
    {
        if (_screenInfoProvider is null || profile.CapturedScreenSnapshot is not { } captured) return false;

        var current = _screenInfoProvider.GetCurrentSnapshot();
        if (captured.IsCompatibleWith(current)) return false;

        ResolutionChangedDuringRun?.Invoke(this, new ResolutionChangedEventArgs(captured, current));
        return true;
    }

    private async Task<ScreenPoint> ExecuteSinglePointAsync(
        ClickPoint point, HumanizationConfig humanization, ScreenPoint? lastCursorPos, Random rng, CancellationToken token)
    {
        var target = humanization.Enabled
            ? PositionJitter.Apply(point.Location, humanization.PositionJitterRadiusPx, rng)
            : point.Location;

        if (humanization.Enabled && humanization.UseCurvedMovement && lastCursorPos is { } from)
        {
            var path = _movementGenerator.GeneratePath(from, target, humanization, rng);
            foreach (var (pathPoint, stepDelayMs) in path)
            {
                token.ThrowIfCancellationRequested();
                _simulator.MoveMouse(pathPoint);
                await CancellableDelay(stepDelayMs, token);
            }
        }
        else
        {
            _simulator.MoveMouse(target);
        }

        for (int i = 0; i < point.ClickCount; i++)
        {
            token.ThrowIfCancellationRequested();
            _simulator.MouseDown(point.Button);
            await CancellableDelay(50, token);
            _simulator.MouseUp(point.Button);
            if (point.ClickCount > 1) await CancellableDelay(80, token);
        }

        return target;
    }

    private static Task CancellableDelay(int ms, CancellationToken token) =>
        ms <= 0 ? Task.CompletedTask : Task.Delay(ms, token);

    private void RaiseStatus(EngineStatus status) =>
        StatusChanged?.Invoke(this, new EngineStatusEventArgs(status));
}
