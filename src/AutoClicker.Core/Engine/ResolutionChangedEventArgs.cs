using AutoClicker.Core.Models;

namespace AutoClicker.Core.Engine;

public sealed class ResolutionChangedEventArgs(ScreenSnapshot previous, ScreenSnapshot current) : EventArgs
{
    public ScreenSnapshot Previous { get; } = previous;
    public ScreenSnapshot Current { get; } = current;
}
