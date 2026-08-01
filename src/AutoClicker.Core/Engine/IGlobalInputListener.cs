using AutoClicker.Core.Models;

namespace AutoClicker.Core.Engine;

public sealed class GlobalHotkeyEventArgs(object subscriberId) : EventArgs
{
    public object SubscriberId { get; } = subscriberId;
}

public interface IGlobalInputListener
{
    event EventHandler<GlobalHotkeyEventArgs>? HotkeyPressed;

    void RegisterHotkey(HotkeyConfig config, object subscriberId);
    void UnregisterHotkey(object subscriberId);

    /// <summary>Zaregistruje jednorázový callback na příští globální kliknutí myší (pro "Přidat bod").</summary>
    IDisposable CaptureNextClick(Action<ScreenPoint> onCaptured);

    /// <summary>Zaregistruje jednorázový callback na příští stisknutou kombinaci kláves (pro nastavení hotkey).</summary>
    IDisposable CaptureNextHotkey(Action<HotkeyConfig> onCaptured);

    void Start();
    void Stop();
}
