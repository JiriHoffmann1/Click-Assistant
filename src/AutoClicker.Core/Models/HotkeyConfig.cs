namespace AutoClicker.Core.Models;

public sealed record HotkeyConfig
{
    public IReadOnlyList<HookKeyCode> Modifiers { get; init; } = Array.Empty<HookKeyCode>();
    public HookKeyCode MainKey { get; init; } = HookKeyCode.F6;
}
