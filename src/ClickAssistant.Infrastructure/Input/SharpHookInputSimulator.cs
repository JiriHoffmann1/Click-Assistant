using ClickAssistant.Core.Engine;
using ClickAssistant.Core.Models;
using SharpHook;
using SharpHook.Data;

namespace ClickAssistant.Infrastructure.Input;

public sealed class SharpHookInputSimulator : IInputSimulator
{
    private readonly EventSimulator _simulator = new();

    public void MoveMouse(ScreenPoint point) =>
        _simulator.SimulateMouseMovement((short)point.X, (short)point.Y);

    public void MouseDown(MouseButtonType button) =>
        _simulator.SimulateMousePress(ToSharpHook(button));

    public void MouseUp(MouseButtonType button) =>
        _simulator.SimulateMouseRelease(ToSharpHook(button));

    private static MouseButton ToSharpHook(MouseButtonType button) => button switch
    {
        MouseButtonType.Left => MouseButton.Button1,
        MouseButtonType.Right => MouseButton.Button2,
        MouseButtonType.Middle => MouseButton.Button3,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };
}
