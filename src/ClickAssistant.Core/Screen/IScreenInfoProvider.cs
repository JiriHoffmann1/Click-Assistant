using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Screen;

public interface IScreenInfoProvider
{
    ScreenSnapshot GetCurrentSnapshot();
}
