using AutoClicker.Core.Models;

namespace AutoClicker.Core.Engine;

public interface IInputSimulator
{
    void MoveMouse(ScreenPoint point);
    void MouseDown(MouseButtonType button);
    void MouseUp(MouseButtonType button);
}
