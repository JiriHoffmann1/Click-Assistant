namespace AutoClicker.Core.Models;

/// <summary>Core-owned podmnožina klávesových kódů, aby Core nemusel záviset na SharpHooku. Infrastructure mapuje na SharpHook.Native.KeyCode.</summary>
public enum HookKeyCode
{
    Ctrl,
    Alt,
    Shift,
    Meta,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Space, Escape, Enter, Tab
}
