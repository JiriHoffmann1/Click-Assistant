using Avalonia.Controls;
using ClickAssistant.Core.Models;
using ClickAssistant.Core.Screen;

namespace ClickAssistant.App.Services;

public sealed class AvaloniaScreenInfoProvider(Window window) : IScreenInfoProvider
{
    public ScreenSnapshot GetCurrentSnapshot()
    {
        var screens = window.Screens?.All ?? [];
        var monitors = screens
            .Select(s => new MonitorBounds(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height, s.Scaling))
            .ToList();

        return new ScreenSnapshot { Monitors = monitors };
    }
}
