namespace AutoClicker.Core.Models;

public sealed record HumanizationConfig
{
    public bool Enabled { get; init; }
    public double PositionJitterRadiusPx { get; init; } = 4.0;
    public bool UseCurvedMovement { get; init; } = true;
    public int MovementDurationMsMin { get; init; } = 120;
    public int MovementDurationMsMax { get; init; } = 380;
    public double CurveBowStrength { get; init; } = 0.25;
    public double OvershootChance { get; init; } = 0.08;
}
