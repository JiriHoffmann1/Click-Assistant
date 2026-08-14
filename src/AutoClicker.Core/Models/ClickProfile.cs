namespace AutoClicker.Core.Models;

public sealed record ClickProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Nový profil";
    public List<ClickPoint> Points { get; init; } = new();
    public SequenceOrderMode OrderMode { get; init; } = SequenceOrderMode.Sequential;
    public List<Guid>? CustomOrder { get; init; }
    public TimingConfig Timing { get; init; } = new();
    public HumanizationConfig Humanization { get; init; } = new();
    public HotkeyConfig StartHotkey { get; init; } = new();
    public HotkeyConfig? StopHotkey { get; init; }
    public ScreenSnapshot? CapturedScreenSnapshot { get; init; }
}
