namespace ClickAssistant.Core.Models;

public readonly record struct MonitorBounds(int X, int Y, int Width, int Height, double Scaling);

public sealed record ScreenSnapshot
{
    public IReadOnlyList<MonitorBounds> Monitors { get; init; } = Array.Empty<MonitorBounds>();

    public bool IsCompatibleWith(ScreenSnapshot other)
    {
        if (Monitors.Count != other.Monitors.Count) return false;
        for (int i = 0; i < Monitors.Count; i++)
        {
            var a = Monitors[i];
            var b = other.Monitors[i];
            if (a.X != b.X || a.Y != b.Y || a.Width != b.Width || a.Height != b.Height) return false;
        }
        return true;
    }
}
