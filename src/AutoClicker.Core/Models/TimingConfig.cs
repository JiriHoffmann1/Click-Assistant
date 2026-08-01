namespace AutoClicker.Core.Models;

public sealed record TimingConfig
{
    public int BaseIntervalMs { get; init; } = 500;
    public int JitterMs { get; init; } = 50;
    public RepeatMode Repeat { get; init; } = RepeatMode.Infinite;
    public int RepeatCount { get; init; } = 1;
}
