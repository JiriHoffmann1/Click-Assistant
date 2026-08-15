using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Screen;

/// <summary>Přepočítá body profilu poměrem starého/nového rozlišení při neshodě ScreenSnapshotu.</summary>
public static class ProfileRescaler
{
    public static ClickProfile Rescale(ClickProfile profile, ScreenSnapshot from, ScreenSnapshot to)
    {
        if (from.Monitors.Count == 0 || to.Monitors.Count == 0) return profile;

        var rescaledPoints = profile.Points
            .Select(p => p with { Location = RescalePoint(p.Location, from, to) })
            .ToList();

        return profile with { Points = rescaledPoints, CapturedScreenSnapshot = to };
    }

    private static ScreenPoint RescalePoint(ScreenPoint point, ScreenSnapshot from, ScreenSnapshot to)
    {
        int monitorIndex = FindMonitorIndex(point, from.Monitors);
        var fromMonitor = from.Monitors[monitorIndex];
        var toMonitor = monitorIndex < to.Monitors.Count ? to.Monitors[monitorIndex] : to.Monitors[0];

        double relativeX = fromMonitor.Width == 0 ? 0 : (double)(point.X - fromMonitor.X) / fromMonitor.Width;
        double relativeY = fromMonitor.Height == 0 ? 0 : (double)(point.Y - fromMonitor.Y) / fromMonitor.Height;

        int newX = toMonitor.X + (int)Math.Round(relativeX * toMonitor.Width);
        int newY = toMonitor.Y + (int)Math.Round(relativeY * toMonitor.Height);
        return new ScreenPoint(newX, newY);
    }

    private static int FindMonitorIndex(ScreenPoint point, IReadOnlyList<MonitorBounds> monitors)
    {
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            if (point.X >= m.X && point.X < m.X + m.Width && point.Y >= m.Y && point.Y < m.Y + m.Height) return i;
        }
        return 0;
    }
}
