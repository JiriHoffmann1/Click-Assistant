namespace AutoClicker.Core.Models;

public sealed record ClickPoint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Bod";
    public ScreenPoint Location { get; init; }
    public MouseButtonType Button { get; init; } = MouseButtonType.Left;
    public int ClickCount { get; init; } = 1;

    /// <summary>Přepíše TimingConfig.BaseIntervalMs jen pro přechod za tímto bodem. Null = použij globální interval.</summary>
    public int? DelayAfterMsOverride { get; init; }
}
