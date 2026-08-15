namespace ClickAssistant.Core.Engine.Jitter;

public static class TimingJitter
{
    private const int MinDelayMs = 10;

    public static int Compute(int baseMs, int jitterMs, Random rng)
    {
        if (jitterMs <= 0) return Math.Max(MinDelayMs, baseMs);
        int offset = rng.Next(-jitterMs, jitterMs + 1);
        return Math.Max(MinDelayMs, baseMs + offset);
    }
}
