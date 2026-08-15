using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Engine;

public interface IInputSimulator
{
    void MoveMouse(ScreenPoint point);
    void MouseDown(MouseButtonType button);
    void MouseUp(MouseButtonType button);
    void KeyDown(HookKeyCode key);
    void KeyUp(HookKeyCode key);
}
