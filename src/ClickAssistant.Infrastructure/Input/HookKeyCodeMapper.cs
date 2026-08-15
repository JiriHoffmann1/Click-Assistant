using ClickAssistant.Core.Models;
using SharpHook.Data;

namespace ClickAssistant.Infrastructure.Input;

/// <summary>Sdílené mapování Core-owned HookKeyCode na SharpHook.Data.KeyCode - používá jak
/// SharpHookGlobalListener (rozpoznávání stisknutých kombinací), tak SharpHookInputSimulator (syntetické stisky).</summary>
internal static class HookKeyCodeMapper
{
    public static KeyCode ToSharpHook(HookKeyCode key) => key switch
    {
        HookKeyCode.Ctrl => KeyCode.VcLeftControl,
        HookKeyCode.Alt => KeyCode.VcLeftAlt,
        HookKeyCode.Shift => KeyCode.VcLeftShift,
        HookKeyCode.Meta => KeyCode.VcLeftMeta,
        HookKeyCode.F1 => KeyCode.VcF1,
        HookKeyCode.F2 => KeyCode.VcF2,
        HookKeyCode.F3 => KeyCode.VcF3,
        HookKeyCode.F4 => KeyCode.VcF4,
        HookKeyCode.F5 => KeyCode.VcF5,
        HookKeyCode.F6 => KeyCode.VcF6,
        HookKeyCode.F7 => KeyCode.VcF7,
        HookKeyCode.F8 => KeyCode.VcF8,
        HookKeyCode.F9 => KeyCode.VcF9,
        HookKeyCode.F10 => KeyCode.VcF10,
        HookKeyCode.F11 => KeyCode.VcF11,
        HookKeyCode.F12 => KeyCode.VcF12,
        HookKeyCode.A => KeyCode.VcA,
        HookKeyCode.B => KeyCode.VcB,
        HookKeyCode.C => KeyCode.VcC,
        HookKeyCode.D => KeyCode.VcD,
        HookKeyCode.E => KeyCode.VcE,
        HookKeyCode.F => KeyCode.VcF,
        HookKeyCode.G => KeyCode.VcG,
        HookKeyCode.H => KeyCode.VcH,
        HookKeyCode.I => KeyCode.VcI,
        HookKeyCode.J => KeyCode.VcJ,
        HookKeyCode.K => KeyCode.VcK,
        HookKeyCode.L => KeyCode.VcL,
        HookKeyCode.M => KeyCode.VcM,
        HookKeyCode.N => KeyCode.VcN,
        HookKeyCode.O => KeyCode.VcO,
        HookKeyCode.P => KeyCode.VcP,
        HookKeyCode.Q => KeyCode.VcQ,
        HookKeyCode.R => KeyCode.VcR,
        HookKeyCode.S => KeyCode.VcS,
        HookKeyCode.T => KeyCode.VcT,
        HookKeyCode.U => KeyCode.VcU,
        HookKeyCode.V => KeyCode.VcV,
        HookKeyCode.W => KeyCode.VcW,
        HookKeyCode.X => KeyCode.VcX,
        HookKeyCode.Y => KeyCode.VcY,
        HookKeyCode.Z => KeyCode.VcZ,
        HookKeyCode.D0 => KeyCode.Vc0,
        HookKeyCode.D1 => KeyCode.Vc1,
        HookKeyCode.D2 => KeyCode.Vc2,
        HookKeyCode.D3 => KeyCode.Vc3,
        HookKeyCode.D4 => KeyCode.Vc4,
        HookKeyCode.D5 => KeyCode.Vc5,
        HookKeyCode.D6 => KeyCode.Vc6,
        HookKeyCode.D7 => KeyCode.Vc7,
        HookKeyCode.D8 => KeyCode.Vc8,
        HookKeyCode.D9 => KeyCode.Vc9,
        HookKeyCode.Space => KeyCode.VcSpace,
        HookKeyCode.Escape => KeyCode.VcEscape,
        HookKeyCode.Enter => KeyCode.VcEnter,
        HookKeyCode.Tab => KeyCode.VcTab,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };
}
