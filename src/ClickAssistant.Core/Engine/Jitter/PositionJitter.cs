using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Engine.Jitter;

public static class PositionJitter
{
    public static ScreenPoint Apply(ScreenPoint center, double radiusPx, Random rng)
    {
        if (radiusPx <= 0) return center;

        double angle = rng.NextDouble() * 2 * Math.PI;
        double r = radiusPx * Math.Sqrt(rng.NextDouble()); // sqrt => rovnoměrné rozdělení po ploše kruhu
        int dx = (int)Math.Round(r * Math.Cos(angle));
        int dy = (int)Math.Round(r * Math.Sin(angle));
        return new ScreenPoint(center.X + dx, center.Y + dy);
    }
}
