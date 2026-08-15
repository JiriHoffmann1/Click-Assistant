using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Engine.Movement;

/// <summary>
/// Generuje humanizovanou trajektorii kurzoru mezi dvěma body: kubická Bézierova křivka
/// s náhodně vychýlenými kontrolními body, ease-in/ease-out časování kroků (ne konstantní
/// rychlost) a volitelný mírný "overshoot" na konci pohybu.
/// </summary>
public sealed class BezierMovementPathGenerator : IMovementPathGenerator
{
    public IReadOnlyList<(ScreenPoint Point, int StepDelayMs)> GeneratePath(
        ScreenPoint start, ScreenPoint end, HumanizationConfig config, Random rng)
    {
        double distance = Distance(start, end);
        if (distance < 1)
        {
            return [(end, 0)];
        }

        var (control1, control2) = BuildControlPoints(start, end, config.CurveBowStrength, rng);

        int steps = Math.Clamp((int)(distance / 6.0), 12, 48);
        int durationMin = Math.Max(0, config.MovementDurationMsMin);
        int durationMax = Math.Max(durationMin, config.MovementDurationMsMax);
        int durationMs = rng.Next(durationMin, durationMax + 1);

        var result = new List<(ScreenPoint, int)>(steps);
        double prevEasedT = 0;
        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;
            double easedT = Easing.EaseInOutCubic(t);
            var point = CubicBezier(start, control1, control2, end, easedT);

            int stepDelay = (int)((easedT - prevEasedT) * durationMs) + rng.Next(-2, 3);
            result.Add((point, Math.Max(0, stepDelay)));
            prevEasedT = easedT;
        }

        if (rng.NextDouble() < config.OvershootChance)
        {
            ApplyOvershootCorrection(result, start, end, rng);
        }

        return result;
    }

    private static (ScreenPoint, ScreenPoint) BuildControlPoints(ScreenPoint a, ScreenPoint b, double bow, Random rng)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double px = -dy / (dist + 1e-6);
        double py = dx / (dist + 1e-6);

        double bulge = dist * bow * (rng.NextDouble() * 0.6 + 0.7);
        double sign = rng.NextDouble() < 0.5 ? -1 : 1;

        var control1 = new ScreenPoint(
            (int)(a.X + dx * 0.33 + px * bulge * sign),
            (int)(a.Y + dy * 0.33 + py * bulge * sign));
        var control2 = new ScreenPoint(
            (int)(a.X + dx * 0.66 + px * bulge * sign * 0.6),
            (int)(a.Y + dy * 0.66 + py * bulge * sign * 0.6));

        return (control1, control2);
    }

    private static ScreenPoint CubicBezier(ScreenPoint p0, ScreenPoint p1, ScreenPoint p2, ScreenPoint p3, double t)
    {
        double u = 1 - t;
        double x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
        double y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
        return new ScreenPoint((int)Math.Round(x), (int)Math.Round(y));
    }

    private static void ApplyOvershootCorrection(List<(ScreenPoint Point, int StepDelayMs)> path, ScreenPoint start, ScreenPoint end, Random rng)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) return;

        double overshootPx = Math.Min(15, dist * 0.05) * (rng.NextDouble() * 0.5 + 0.5);
        var overshotPoint = new ScreenPoint(
            end.X + (int)Math.Round(dx / dist * overshootPx),
            end.Y + (int)Math.Round(dy / dist * overshootPx));

        path.Add((overshotPoint, rng.Next(15, 35)));
        path.Add((end, rng.Next(20, 45)));
    }

    private static double Distance(ScreenPoint a, ScreenPoint b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
}
