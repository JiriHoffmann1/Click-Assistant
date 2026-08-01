using AutoClicker.Core.Models;

namespace AutoClicker.Core.Screen;

public interface IScreenInfoProvider
{
    ScreenSnapshot GetCurrentSnapshot();
}
