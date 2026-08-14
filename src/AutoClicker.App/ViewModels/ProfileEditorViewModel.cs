using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AutoClicker.App.Localization;
using AutoClicker.Core.Engine;
using AutoClicker.Core.Models;
using AutoClicker.Core.Screen;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoClicker.App.ViewModels;

public partial class ProfileEditorViewModel : ObservableObject
{
    public const double MapWidth = 380;
    public const double MapHeight = 200;
    public const int DetailCaptureWidth = 220;
    public const int DetailCaptureHeight = 140;

    private readonly IGlobalInputListener _globalListener;
    private readonly IScreenInfoProvider _screenInfoProvider;
    private readonly IScreenCaptureProvider _screenCaptureProvider;
    private IDisposable? _activeCapture;
    private HotkeyConfig _hotkey = new();
    private HotkeyConfig? _stopHotkey;
    private DispatcherTimer? _captureDebounceTimer;

    public Guid ProfileId { get; private set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = LocalizationManager.Instance["profile.defaultName"];

    [ObservableProperty]
    private SequenceOrderMode _orderMode = SequenceOrderMode.Sequential;

    [ObservableProperty]
    private decimal _baseIntervalMs = 500;

    [ObservableProperty]
    private decimal _jitterMs = 50;

    [ObservableProperty]
    private RepeatMode _repeat = RepeatMode.Infinite;

    [ObservableProperty]
    private decimal _repeatCount = 1;

    [ObservableProperty]
    private bool _isCapturingPoint;

    [ObservableProperty]
    private SequenceStepViewModel? _selectedStep;

    [ObservableProperty]
    private bool _humanizationEnabled;

    [ObservableProperty]
    private decimal _positionJitterRadiusPx = 4;

    [ObservableProperty]
    private bool _useCurvedMovement = true;

    [ObservableProperty]
    private decimal _movementDurationMsMin = 120;

    [ObservableProperty]
    private decimal _movementDurationMsMax = 380;

    [ObservableProperty]
    private decimal _curveBowStrengthPercent = 25;

    [ObservableProperty]
    private decimal _overshootChancePercent = 8;

    [ObservableProperty]
    private string _hotkeyDisplayText = "F6";

    [ObservableProperty]
    private bool _isCapturingHotkey;

    [ObservableProperty]
    private string _stopHotkeyDisplayText = LocalizationManager.Instance["hotkey.notSet"];

    [ObservableProperty]
    private bool _isCapturingStopHotkey;

    [ObservableProperty]
    private bool _hasStopHotkey;

    [ObservableProperty]
    private bool _showRealScreenshot;

    [ObservableProperty]
    private Bitmap? _detailBitmap;

    public ObservableCollection<SequenceStepViewModel> Steps { get; } = new();

    public ObservableCollection<MapMonitorRectViewModel> MapMonitorRects { get; } = new();
    public ObservableCollection<Point> MapPolylinePoints { get; } = new();

    public ScreenSnapshot? CapturedScreenSnapshot { get; private set; }

    public IReadOnlyList<SequenceOrderMode> OrderModeValues { get; } = Enum.GetValues<SequenceOrderMode>();
    public IReadOnlyList<RepeatMode> RepeatModeValues { get; } = Enum.GetValues<RepeatMode>();

    /// <summary>Vyvoláno při načtení profilu i po zachycení nové kombinace, ať MainWindowViewModel může přeregistrovat globální hotkey.</summary>
    public event Action<HotkeyConfig>? HotkeyChanged;

    /// <summary>Vyvoláno při načtení profilu i po zachycení nové kombinace pro Stop klávesu (null = nenastaveno).</summary>
    public event Action<HotkeyConfig?>? StopHotkeyChanged;

    public ProfileEditorViewModel(IGlobalInputListener globalListener, IScreenInfoProvider screenInfoProvider, IScreenCaptureProvider screenCaptureProvider)
    {
        _globalListener = globalListener;
        _screenInfoProvider = screenInfoProvider;
        _screenCaptureProvider = screenCaptureProvider;
        _hotkey = new HotkeyConfig();
        HotkeyDisplayText = FormatHotkey(_hotkey);
    }

    partial void OnSelectedStepChanged(SequenceStepViewModel? value)
    {
        _captureDebounceTimer?.Stop();
        RefreshDetailCapture();
    }

    partial void OnShowRealScreenshotChanged(bool value)
    {
        _captureDebounceTimer?.Stop();
        RefreshDetailCapture();
    }

    /// <summary>
    /// Naplánuje obnovu náhledu s krátkým zpožděním místo okamžitého zachycení. X/Y v SequenceTimelineView
    /// se aktualizují živě při psaní (NumericUpDown), takže bez debounce by každý stisk klávesy spustil
    /// GDI CopyFromScreen + PNG enkódování na UI vlákně - znatelné zadrhávání při rychlém přepisování souřadnic.
    /// </summary>
    private void ScheduleDetailCaptureRefresh()
    {
        if (_captureDebounceTimer is null)
        {
            _captureDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _captureDebounceTimer.Tick += (_, _) =>
            {
                _captureDebounceTimer!.Stop();
                RefreshDetailCapture();
            };
        }

        _captureDebounceTimer.Stop();
        _captureDebounceTimer.Start();
    }

    private void RefreshDetailCapture()
    {
        DetailBitmap?.Dispose();
        DetailBitmap = null;

        if (!ShowRealScreenshot || SelectedStep is null) return;

        var png = _screenCaptureProvider.CaptureRegion(
            (int)SelectedStep.X - DetailCaptureWidth / 2,
            (int)SelectedStep.Y - DetailCaptureHeight / 2,
            DetailCaptureWidth,
            DetailCaptureHeight);
        if (png is null) return;

        using var stream = new MemoryStream(png);
        DetailBitmap = new Bitmap(stream);
    }

    public void LoadFrom(ClickProfile profile)
    {
        SelectedStep = null;
        ProfileId = profile.Id;
        Name = profile.Name;
        OrderMode = profile.OrderMode;
        BaseIntervalMs = profile.Timing.BaseIntervalMs;
        JitterMs = profile.Timing.JitterMs;
        Repeat = profile.Timing.Repeat;
        RepeatCount = profile.Timing.RepeatCount;
        CapturedScreenSnapshot = profile.CapturedScreenSnapshot;

        HumanizationEnabled = profile.Humanization.Enabled;
        PositionJitterRadiusPx = (decimal)profile.Humanization.PositionJitterRadiusPx;
        UseCurvedMovement = profile.Humanization.UseCurvedMovement;
        MovementDurationMsMin = profile.Humanization.MovementDurationMsMin;
        MovementDurationMsMax = profile.Humanization.MovementDurationMsMax;
        CurveBowStrengthPercent = (decimal)(profile.Humanization.CurveBowStrength * 100);
        OvershootChancePercent = (decimal)(profile.Humanization.OvershootChance * 100);

        _hotkey = profile.StartHotkey;
        HotkeyDisplayText = FormatHotkey(_hotkey);
        HotkeyChanged?.Invoke(_hotkey);

        _stopHotkey = profile.StopHotkey;
        HasStopHotkey = _stopHotkey is not null;
        StopHotkeyDisplayText = _stopHotkey is { } stopHotkey ? FormatHotkey(stopHotkey) : LocalizationManager.Instance["hotkey.notSet"];
        StopHotkeyChanged?.Invoke(_stopHotkey);

        ClearSteps();
        var order = profile.CustomOrder is { Count: > 0 }
            ? profile.CustomOrder.Select(id => profile.Points.FirstOrDefault(p => p.Id == id)).Where(p => p is not null).Select(p => p!)
            : profile.Points;
        foreach (var point in order) AddStep(new SequenceStepViewModel(point));
        RenumberSteps();
    }

    public void ResetToNewProfile()
    {
        ProfileId = Guid.NewGuid();
        Name = LocalizationManager.Instance["profile.defaultName"];
        OrderMode = SequenceOrderMode.Sequential;
        BaseIntervalMs = 500;
        JitterMs = 50;
        Repeat = RepeatMode.Infinite;
        RepeatCount = 1;
        CapturedScreenSnapshot = null;
        HumanizationEnabled = false;
        PositionJitterRadiusPx = 4;
        UseCurvedMovement = true;
        MovementDurationMsMin = 120;
        MovementDurationMsMax = 380;
        CurveBowStrengthPercent = 25;
        OvershootChancePercent = 8;
        _hotkey = new HotkeyConfig();
        HotkeyDisplayText = FormatHotkey(_hotkey);
        HotkeyChanged?.Invoke(_hotkey);
        _stopHotkey = null;
        HasStopHotkey = false;
        StopHotkeyDisplayText = LocalizationManager.Instance["hotkey.notSet"];
        StopHotkeyChanged?.Invoke(null);
        ClearSteps();
        SelectedStep = null;
        RecomputeMap();
    }

    public ClickProfile ToClickProfile(ScreenSnapshot? currentSnapshot = null) => new()
    {
        Id = ProfileId,
        Name = Name,
        Points = Steps.Select(s => s.ToClickPoint()).ToList(),
        OrderMode = OrderMode,
        CustomOrder = Steps.Select(s => s.Id).ToList(),
        Timing = new TimingConfig
        {
            BaseIntervalMs = (int)BaseIntervalMs,
            JitterMs = (int)JitterMs,
            Repeat = Repeat,
            RepeatCount = (int)RepeatCount
        },
        Humanization = new HumanizationConfig
        {
            Enabled = HumanizationEnabled,
            PositionJitterRadiusPx = (double)PositionJitterRadiusPx,
            UseCurvedMovement = UseCurvedMovement,
            MovementDurationMsMin = (int)MovementDurationMsMin,
            MovementDurationMsMax = (int)Math.Max(MovementDurationMsMin, MovementDurationMsMax),
            CurveBowStrength = (double)(CurveBowStrengthPercent / 100),
            OvershootChance = (double)(OvershootChancePercent / 100)
        },
        StartHotkey = _hotkey,
        StopHotkey = _stopHotkey,
        CapturedScreenSnapshot = currentSnapshot ?? CapturedScreenSnapshot
    };

    [RelayCommand]
    private void AddPoint()
    {
        if (IsCapturingPoint || IsCapturingHotkey || IsCapturingStopHotkey) return;

        IsCapturingPoint = true;
        _activeCapture = _globalListener.CaptureNextClick(point =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var step = new SequenceStepViewModel(new ClickPoint
                {
                    Name = string.Format(LocalizationManager.Instance["point.defaultNameFormat"], Steps.Count + 1),
                    Location = point
                });
                AddStep(step);
                RenumberSteps();
                CapturedScreenSnapshot ??= _screenInfoProvider.GetCurrentSnapshot();
                IsCapturingPoint = false;
                _activeCapture = null;
            });
        });
    }

    [RelayCommand]
    private void CancelCapture()
    {
        _activeCapture?.Dispose();
        _activeCapture = null;
        IsCapturingPoint = false;
    }

    [RelayCommand]
    private void SetHotkey()
    {
        if (IsCapturingHotkey || IsCapturingPoint || IsCapturingStopHotkey) return;

        IsCapturingHotkey = true;
        _activeCapture = _globalListener.CaptureNextHotkey(hotkey =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _hotkey = hotkey;
                HotkeyDisplayText = FormatHotkey(hotkey);
                HotkeyChanged?.Invoke(hotkey);
                IsCapturingHotkey = false;
                _activeCapture = null;
            });
        });
    }

    [RelayCommand]
    private void CancelHotkeyCapture()
    {
        _activeCapture?.Dispose();
        _activeCapture = null;
        IsCapturingHotkey = false;
    }

    [RelayCommand]
    private void SetStopHotkey()
    {
        if (IsCapturingHotkey || IsCapturingPoint || IsCapturingStopHotkey) return;

        IsCapturingStopHotkey = true;
        _activeCapture = _globalListener.CaptureNextHotkey(hotkey =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _stopHotkey = hotkey;
                HasStopHotkey = true;
                StopHotkeyDisplayText = FormatHotkey(hotkey);
                StopHotkeyChanged?.Invoke(hotkey);
                IsCapturingStopHotkey = false;
                _activeCapture = null;
            });
        });
    }

    [RelayCommand]
    private void CancelStopHotkeyCapture()
    {
        _activeCapture?.Dispose();
        _activeCapture = null;
        IsCapturingStopHotkey = false;
    }

    [RelayCommand]
    private void RemoveStep(SequenceStepViewModel? step)
    {
        step ??= SelectedStep;
        if (step is null) return;
        step.PropertyChanged -= OnStepPositionChanged;
        Steps.Remove(step);
        if (SelectedStep == step) SelectedStep = null;
        RenumberSteps();
    }

    [RelayCommand]
    private void SelectStep(SequenceStepViewModel? step)
    {
        SelectedStep = step;
    }

    [RelayCommand]
    private void MoveStepUp(SequenceStepViewModel? step)
    {
        if (step is null) return;
        var index = Steps.IndexOf(step);
        if (index <= 0) return;
        Steps.Move(index, index - 1);
        RenumberSteps();
    }

    [RelayCommand]
    private void MoveStepDown(SequenceStepViewModel? step)
    {
        if (step is null) return;
        var index = Steps.IndexOf(step);
        if (index < 0 || index >= Steps.Count - 1) return;
        Steps.Move(index, index + 1);
        RenumberSteps();
    }

    private void RenumberSteps()
    {
        for (int i = 0; i < Steps.Count; i++) Steps[i].StepNumber = i + 1;
        RecomputeMap();
    }

    private void AddStep(SequenceStepViewModel step)
    {
        step.PropertyChanged += OnStepPositionChanged;
        Steps.Add(step);
    }

    private void ClearSteps()
    {
        foreach (var step in Steps) step.PropertyChanged -= OnStepPositionChanged;
        Steps.Clear();
    }

    private void OnStepPositionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SequenceStepViewModel.X) or nameof(SequenceStepViewModel.Y))) return;

        RecomputeMap();
        if (ReferenceEquals(sender, SelectedStep)) ScheduleDetailCaptureRefresh();
    }

    private void RecomputeMap()
    {
        var snapshot = CapturedScreenSnapshot ?? _screenInfoProvider.GetCurrentSnapshot();
        MapMonitorRects.Clear();
        MapPolylinePoints.Clear();

        if (snapshot.Monitors.Count == 0) return;

        int minX = snapshot.Monitors.Min(m => m.X);
        int minY = snapshot.Monitors.Min(m => m.Y);
        int maxX = snapshot.Monitors.Max(m => m.X + m.Width);
        int maxY = snapshot.Monitors.Max(m => m.Y + m.Height);
        double totalW = Math.Max(1, maxX - minX);
        double totalH = Math.Max(1, maxY - minY);
        double scale = Math.Min(MapWidth / totalW, MapHeight / totalH);
        double offsetX = (MapWidth - totalW * scale) / 2;
        double offsetY = (MapHeight - totalH * scale) / 2;

        double ToMapX(double worldX) => offsetX + (worldX - minX) * scale;
        double ToMapY(double worldY) => offsetY + (worldY - minY) * scale;

        foreach (var monitor in snapshot.Monitors)
        {
            MapMonitorRects.Add(new MapMonitorRectViewModel
            {
                X = ToMapX(monitor.X),
                Y = ToMapY(monitor.Y),
                Width = monitor.Width * scale,
                Height = monitor.Height * scale
            });
        }

        foreach (var step in Steps)
        {
            step.MapX = ToMapX((double)step.X);
            step.MapY = ToMapY((double)step.Y);
            MapPolylinePoints.Add(new Point(step.MapX, step.MapY));
        }
    }

    private static string FormatHotkey(HotkeyConfig hotkey)
    {
        var parts = hotkey.Modifiers.Select(m => m.ToString()).Append(hotkey.MainKey.ToString());
        return string.Join("+", parts);
    }
}
