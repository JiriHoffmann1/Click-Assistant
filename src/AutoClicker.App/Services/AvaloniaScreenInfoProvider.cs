using Avalonia.Controls;
using AutoClicker.Core.Models;
using AutoClicker.Core.Screen;

namespace AutoClicker.App.Services;

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
