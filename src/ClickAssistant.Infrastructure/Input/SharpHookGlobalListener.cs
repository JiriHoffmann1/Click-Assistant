using System.Collections.Concurrent;
using ClickAssistant.Core.Engine;
using ClickAssistant.Core.Models;
using SharpHook;
using SharpHook.Data;

namespace ClickAssistant.Infrastructure.Input;

public sealed class SharpHookGlobalListener : IGlobalInputListener, IDisposable
{
    private readonly TaskPoolGlobalHook _hook = new();
    private readonly HashSet<KeyCode> _pressedKeys = new();
    private readonly ConcurrentDictionary<object, (HotkeyConfig Config, bool Triggered)> _hotkeys = new();
    private Action<ScreenPoint>? _pendingCaptureCallback;
    private Action<HotkeyConfig>? _pendingHotkeyCaptureCallback;

    public event EventHandler<GlobalHotkeyEventArgs>? HotkeyPressed;

    public SharpHookGlobalListener()
    {
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _hook.MouseClicked += OnMouseClicked;
    }

    public void Start() => _hook.RunAsync();

    public void Stop() => _hook.Dispose();

    public void RegisterHotkey(HotkeyConfig config, object subscriberId) =>
        _hotkeys[subscriberId] = (config, false);

    public void UnregisterHotkey(object subscriberId) =>
        _hotkeys.TryRemove(subscriberId, out _);

    public IDisposable CaptureNextClick(Action<ScreenPoint> onCaptured)
    {
        _pendingCaptureCallback = onCaptured;
        return new CaptureCancellation(() => _pendingCaptureCallback = null);
    }

    public IDisposable CaptureNextHotkey(Action<HotkeyConfig> onCaptured)
    {
        _pendingHotkeyCaptureCallback = onCaptured;
        return new CaptureCancellation(() => _pendingHotkeyCaptureCallback = null);
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        lock (_pressedKeys) _pressedKeys.Add(e.Data.KeyCode);

        if (_pendingHotkeyCaptureCallback is not null && TryMapMainKey(e.Data.KeyCode, out var mainKey))
        {
            HashSet<KeyCode> snapshot;
            lock (_pressedKeys) snapshot = new HashSet<KeyCode>(_pressedKeys);

            var modifiers = new List<HookKeyCode>();
            if (snapshot.Contains(KeyCode.VcLeftControl) || snapshot.Contains(KeyCode.VcRightControl)) modifiers.Add(HookKeyCode.Ctrl);
            if (snapshot.Contains(KeyCode.VcLeftAlt) || snapshot.Contains(KeyCode.VcRightAlt)) modifiers.Add(HookKeyCode.Alt);
            if (snapshot.Contains(KeyCode.VcLeftShift) || snapshot.Contains(KeyCode.VcRightShift)) modifiers.Add(HookKeyCode.Shift);
            if (snapshot.Contains(KeyCode.VcLeftMeta) || snapshot.Contains(KeyCode.VcRightMeta)) modifiers.Add(HookKeyCode.Meta);

            var callback = Interlocked.Exchange(ref _pendingHotkeyCaptureCallback, null);
            callback?.Invoke(new HotkeyConfig { Modifiers = modifiers, MainKey = mainKey });
        }

        foreach (var (id, entry) in _hotkeys)
        {
            if (entry.Triggered) continue;
            bool matches;
            lock (_pressedKeys) matches = Matches(entry.Config, _pressedKeys);
            if (!matches) continue;

            // TaskPoolGlobalHook dispatches on the thread pool and doesn't guarantee KeyPressed
            // handlers run one at a time, so a held key's OS auto-repeat can invoke OnKeyPressed
            // concurrently on two pool threads. A plain read-then-write here let both see
            // Triggered==false and both raise HotkeyPressed for what should be one press.
            // TryUpdate's compare-and-swap makes only one of them win the flip.
            if (_hotkeys.TryUpdate(id, (entry.Config, true), (entry.Config, false)))
                HotkeyPressed?.Invoke(this, new GlobalHotkeyEventArgs(id));
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        lock (_pressedKeys) _pressedKeys.Remove(e.Data.KeyCode);

        foreach (var id in _hotkeys.Keys)
        {
            if (_hotkeys.TryGetValue(id, out var entry))
                _hotkeys[id] = (entry.Config, false);
        }
    }

    private void OnMouseClicked(object? sender, MouseHookEventArgs e)
    {
        var callback = Interlocked.Exchange(ref _pendingCaptureCallback, null);
        callback?.Invoke(new ScreenPoint(e.Data.X, e.Data.Y));
    }

    private static bool Matches(HotkeyConfig config, HashSet<KeyCode> pressed)
    {
        foreach (var modifier in config.Modifiers)
        {
            if (!pressed.Contains(ToSharpHook(modifier))) return false;
        }
        return pressed.Contains(ToSharpHook(config.MainKey));
    }

    private static KeyCode ToSharpHook(HookKeyCode key) => HookKeyCodeMapper.ToSharpHook(key);

    private static readonly HookKeyCode[] MainKeyCandidates = Enum.GetValues<HookKeyCode>()
        .Where(k => k is not (HookKeyCode.Ctrl or HookKeyCode.Alt or HookKeyCode.Shift or HookKeyCode.Meta))
        .ToArray();

    private static bool TryMapMainKey(KeyCode code, out HookKeyCode mainKey)
    {
        foreach (var candidate in MainKeyCandidates)
        {
            if (ToSharpHook(candidate) == code)
            {
                mainKey = candidate;
                return true;
            }
        }
        mainKey = default;
        return false;
    }

    public void Dispose() => _hook.Dispose();

    private sealed class CaptureCancellation(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
