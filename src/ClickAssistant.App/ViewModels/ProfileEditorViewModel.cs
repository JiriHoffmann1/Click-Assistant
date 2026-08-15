using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClickAssistant.App.Localization;
using ClickAssistant.Core.Engine;
using ClickAssistant.Core.Models;
using ClickAssistant.Core.Screen;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickAssistant.App.ViewModels;

public partial class ProfileEditorViewModel : ObservableObject
{
    private const double DefaultMapWidth = 380;
    private const double DefaultMapHeight = 200;
    public const int DetailCaptureWidth = 220;
    public const int DetailCaptureHeight = 140;

    private readonly IGlobalInputListener _globalListener;
    private readonly IScreenInfoProvider _screenInfoProvider;
    private readonly IScreenCaptureProvider _screenCaptureProvider;
    private readonly IMouseInfoProvider _mouseInfoProvider;
    private IDisposable? _activeCapture;
    private HotkeyConfig _hotkey = new();
    private HotkeyConfig? _stopHotkey;
    private DispatcherTimer? _captureDebounceTimer;
    private DispatcherTimer? _identifyMonitorsTimer;

    /// <summary>Ruční pozice monitorů v mapě (klíč = Index monitoru), když je uživatel po zobrazení
    /// přetáhl na jiné místo než automatický layout - viz UpdateMonitorManualPosition.</summary>
    private readonly Dictionary<int, (double X, double Y)> _manualMonitorPositions = new();

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

    /// <summary>Index monitoru, na který je mapa aktuálně přiblížená (null = zobrazit všechny monitory najednou).</summary>
    [ObservableProperty]
    private int? _focusedMonitorIndex;

    [ObservableProperty]
    private ObservableCollection<SequenceStepViewModel> _steps = new();

    [ObservableProperty]
    private ObservableCollection<MapMonitorRectViewModel> _mapMonitorRects = new();

    [ObservableProperty]
    private ObservableCollection<Point> _mapPolylinePoints = new();

    /// <summary>Rozměr mapy se přizpůsobuje počtu/uspořádání monitorů (viz RecomputeMap) - roste do šířky
    /// i výšky, pokud se monitory nevejdou do výchozí velikosti nebo je jich víc řádků.</summary>
    [ObservableProperty]
    private double _mapWidth = DefaultMapWidth;

    [ObservableProperty]
    private double _mapHeight = DefaultMapHeight;

    /// <summary>Dočasně zobrazí velké číslo monitoru přes jeho obdélník v mapě (jako "Identify" v nastavení
    /// displejů Windows), aby uživatel poznal, který fyzický monitor odpovídá kterému obdélníku.</summary>
    [ObservableProperty]
    private bool _isIdentifyingMonitors;

    public ScreenSnapshot? CapturedScreenSnapshot { get; private set; }

    public IReadOnlyList<SequenceOrderMode> OrderModeValues { get; } = Enum.GetValues<SequenceOrderMode>();
    public IReadOnlyList<RepeatMode> RepeatModeValues { get; } = Enum.GetValues<RepeatMode>();

    /// <summary>Vyvoláno při načtení profilu i po zachycení nové kombinace, ať MainWindowViewModel může přeregistrovat globální hotkey.</summary>
    public event Action<HotkeyConfig>? HotkeyChanged;

    /// <summary>Vyvoláno při načtení profilu i po zachycení nové kombinace pro Stop klávesu (null = nenastaveno).</summary>
    public event Action<HotkeyConfig?>? StopHotkeyChanged;

    public ProfileEditorViewModel(
        IGlobalInputListener globalListener, IScreenInfoProvider screenInfoProvider,
        IScreenCaptureProvider screenCaptureProvider, IMouseInfoProvider mouseInfoProvider)
    {
        _globalListener = globalListener;
        _screenInfoProvider = screenInfoProvider;
        _screenCaptureProvider = screenCaptureProvider;
        _mouseInfoProvider = mouseInfoProvider;
        _hotkey = new HotkeyConfig();
        HotkeyDisplayText = FormatHotkey(_hotkey);
    }

    partial void OnSelectedStepChanged(SequenceStepViewModel? oldValue, SequenceStepViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;

        _captureDebounceTimer?.Stop();
        RefreshDetailCapture();
    }

    partial void OnShowRealScreenshotChanged(bool value)
    {
        _captureDebounceTimer?.Stop();
        RefreshDetailCapture();
    }

    partial void OnHumanizationEnabledChanged(bool value) => OnPropertyChanged(nameof(ShowJitterCircle));

    partial void OnPositionJitterRadiusPxChanged(decimal value) => OnPropertyChanged(nameof(DetailJitterDisplayDiameter));

    partial void OnDetailBitmapChanged(Bitmap? value) => OnPropertyChanged(nameof(ShowJitterCircle));

    /// <summary>Průměr kroužku odchylky v detailu screenshotu, ve stejném 2x přiblíženém měřítku jako obrázek (viz DetailCaptureWidth/Height).</summary>
    public double DetailJitterDisplayDiameter => (double)PositionJitterRadiusPx * 4;

    public bool ShowJitterCircle => HumanizationEnabled && DetailBitmap is not null;

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
        FocusedMonitorIndex = null;
        _manualMonitorPositions.Clear();
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
        var loadedSteps = order.Select(point => new SequenceStepViewModel(point)).ToList();
        foreach (var step in loadedSteps)
        {
            step.PropertyChanged += OnStepPropertyChanged;
            if (step.ActionType == StepActionType.MouseClick) RefreshMouseButtonOptions(step);
        }
        // Nahradit celou kolekci najednou (jedna notifikace) místo Clear() + postupných Add() -
        // ItemsControl v SequenceMapView.axaml na Avalonii spolehlivě vykreslí jen první přidanou
        // položku po Clear()+více Add() ve stejném volání, zbytek zůstane neviditelný.
        Steps = new ObservableCollection<SequenceStepViewModel>(loadedSteps);
        RenumberSteps();
    }

    public void ResetToNewProfile()
    {
        _manualMonitorPositions.Clear();
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
        FocusedMonitorIndex = null;
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
        if (IsCapturingPoint || IsCapturingHotkey || IsCapturingStopHotkey || _capturingKeyStep is not null) return;

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

    /// <summary>Přidá krok, který místo kliknutí myší stiskne klávesu - nepotřebuje zachycenou pozici na obrazovce.</summary>
    [RelayCommand]
    private void AddKeyStep()
    {
        if (IsCapturingPoint || IsCapturingHotkey || IsCapturingStopHotkey || _capturingKeyStep is not null) return;

        var step = new SequenceStepViewModel(new ClickPoint
        {
            Name = string.Format(LocalizationManager.Instance["point.defaultNameFormat"], Steps.Count + 1),
            ActionType = StepActionType.KeyPress
        });
        AddStep(step);
        RenumberSteps();
    }

    private SequenceStepViewModel? _capturingKeyStep;

    [RelayCommand]
    private void CaptureStepKey(SequenceStepViewModel? step)
    {
        if (step is null || IsCapturingPoint || IsCapturingHotkey || IsCapturingStopHotkey || _capturingKeyStep is not null) return;

        _capturingKeyStep = step;
        step.IsCapturingKey = true;
        _activeCapture = _globalListener.CaptureNextHotkey(hotkey =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                step.Key = hotkey.MainKey;
                step.KeyDisplayText = hotkey.MainKey.ToString();
                step.IsCapturingKey = false;
                _capturingKeyStep = null;
                _activeCapture = null;
            });
        });
    }

    [RelayCommand]
    private void CancelStepKeyCapture()
    {
        _activeCapture?.Dispose();
        _activeCapture = null;
        if (_capturingKeyStep is not null) _capturingKeyStep.IsCapturingKey = false;
        _capturingKeyStep = null;
    }

    [RelayCommand]
    private void SetHotkey()
    {
        if (IsCapturingHotkey || IsCapturingPoint || IsCapturingStopHotkey || _capturingKeyStep is not null) return;

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
        if (IsCapturingHotkey || IsCapturingPoint || IsCapturingStopHotkey || _capturingKeyStep is not null) return;

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
        step.PropertyChanged -= OnStepPropertyChanged;
        Steps.Remove(step);
        if (SelectedStep == step) SelectedStep = null;
        RenumberSteps();
    }

    [RelayCommand]
    private void SelectStep(SequenceStepViewModel? step)
    {
        SelectedStep = step;
    }

    /// <summary>Na pár sekund zobrazí velké číslo přes každý obdélník monitoru v mapě a zároveň skutečný
    /// fullscreen overlay na každém fyzickém monitoru (SequenceMapView reaguje na IsIdentifyingMonitors),
    /// ať uživatel pozná, který fyzický monitor je který (obdoba "Identify" v nastavení displejů Windows).
    /// Druhé kliknutí, dokud identifikace běží, ji rovnou vypne.</summary>
    [RelayCommand]
    private void IdentifyMonitors()
    {
        if (IsIdentifyingMonitors)
        {
            StopIdentifyingMonitors();
            return;
        }

        IsIdentifyingMonitors = true;
        _identifyMonitorsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _identifyMonitorsTimer.Tick += (_, _) => StopIdentifyingMonitors();
        _identifyMonitorsTimer.Start();
    }

    private void StopIdentifyingMonitors()
    {
        _identifyMonitorsTimer?.Stop();
        _identifyMonitorsTimer = null;
        IsIdentifyingMonitors = false;
    }

    /// <summary>Aktuální reálné monitory (fyzické souřadnice/rozlišení), pro potřeby fullscreen "Identify"
    /// overlaye ve View - stejný zdroj snapshotu jako RecomputeMap.</summary>
    public IReadOnlyList<MonitorBounds> GetCurrentMonitors() =>
        (CapturedScreenSnapshot ?? _screenInfoProvider.GetCurrentSnapshot()).Monitors;

    /// <summary>Zavolá se z code-behind mapy při přetahování obdélníku monitoru myší - uloží ruční pozici
    /// a přepočítá mapu, ať se spolu s monitorem přesunou i jeho body a čáry mezi nimi.</summary>
    public void UpdateMonitorManualPosition(int monitorIndex, double x, double y)
    {
        _manualMonitorPositions[monitorIndex] = (x, y);
        RecomputeMap();
    }

    /// <summary>Klik na obdélník monitoru v mapě - přiblíží mapu na ten jeden monitor (užitečné, když body leží
    /// jen na jednom z více monitorů). Druhý klik na už přiblížený monitor mapu zase oddálí na všechny monitory.</summary>
    [RelayCommand]
    private void ToggleMonitorFocus(int monitorIndex)
    {
        FocusedMonitorIndex = FocusedMonitorIndex == monitorIndex ? null : monitorIndex;
        RecomputeMap();
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
        step.PropertyChanged += OnStepPropertyChanged;
        Steps.Add(step);
        if (step.ActionType == StepActionType.MouseClick) RefreshMouseButtonOptions(step);
    }

    private void ClearSteps()
    {
        foreach (var step in Steps) step.PropertyChanged -= OnStepPropertyChanged;
        Steps.Clear();
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SequenceStepViewModel step) return;

        if (e.PropertyName is nameof(SequenceStepViewModel.X) or nameof(SequenceStepViewModel.Y))
        {
            RecomputeMap();
            if (ReferenceEquals(sender, SelectedStep)) ScheduleDetailCaptureRefresh();
        }
        // Uživatel právě u tohoto kroku přepnul akci na "klik myší" - znovu zjistit, kolik tlačítek
        // aktuálně připojená myš má, ať mu nabídneme jen ta, co skutečně existují.
        else if (e.PropertyName == nameof(SequenceStepViewModel.ActionType) && step.ActionType == StepActionType.MouseClick)
        {
            RefreshMouseButtonOptions(step);
        }
    }

    /// <summary>Zjistí aktuální počet tlačítek myši a omezí na ně nabídku v ComboBoxu tlačítka u kroku.</summary>
    private void RefreshMouseButtonOptions(SequenceStepViewModel step)
    {
        int buttonCount = _mouseInfoProvider.GetButtonCount();
        var available = new List<MouseButtonType> { MouseButtonType.Left, MouseButtonType.Right, MouseButtonType.Middle };
        if (buttonCount >= 4) available.Add(MouseButtonType.Back);
        if (buttonCount >= 5) available.Add(MouseButtonType.Forward);

        step.MouseButtonValues = available;
        if (!available.Contains(step.Button)) step.Button = MouseButtonType.Left;
    }

    private const double MonitorGapPx = 6;
    private const double MonitorRowGapPx = 10;
    private const double StepBadgeSize = 18;
    private const double MonitorMapPaddingPx = 6;
    private const double MaxOverviewRowWidth = 460;

    private void RecomputeMap()
    {
        var snapshot = CapturedScreenSnapshot ?? _screenInfoProvider.GetCurrentSnapshot();

        if (snapshot.Monitors.Count == 0)
        {
            MapMonitorRects = new ObservableCollection<MapMonitorRectViewModel>();
            MapPolylinePoints = new ObservableCollection<Point>();
            return;
        }

        if (FocusedMonitorIndex is int fi && (fi < 0 || fi >= snapshot.Monitors.Count)) FocusedMonitorIndex = null;

        var rects = new List<MapMonitorRectViewModel>();
        Func<double, double, (double X, double Y)> toMap;
        if (FocusedMonitorIndex is int idx)
        {
            MapWidth = DefaultMapWidth;
            MapHeight = DefaultMapHeight;
            toMap = BuildFocusedMap(snapshot.Monitors[idx], idx, MapWidth, MapHeight, rects);
        }
        else
        {
            toMap = BuildOverviewMap(snapshot.Monitors, rects, out var canvasWidth, out var canvasHeight);
            MapWidth = canvasWidth;
            MapHeight = canvasHeight;
        }

        // Nahradit celou kolekci najednou (jedna notifikace) místo Clear() + postupných Add() -
        // ItemsControl v SequenceMapView.axaml na Avalonii spolehlivě vykreslí jen první přidanou
        // položku po Clear()+více Add() ve stejném volání, zbytek zůstane neviditelný.
        MapMonitorRects = new ObservableCollection<MapMonitorRectViewModel>(rects);

        var polylinePoints = new List<Point>();
        foreach (var step in Steps)
        {
            var (mx, my) = toMap((double)step.X, (double)step.Y);
            // (mx,my) je skutečný namapovaný bod (střed tečky). Canvas.Left/Top umisťuje LEVÝ HORNÍ roh
            // tečky (Button 18x18), takže MapX/MapY musí být posunuté o polovinu velikosti tečky zpět -
            // jinak tečka trčí vpravo/dole od skutečné pozice a u okraje monitoru přesahuje mimo jeho obdélník.
            step.MapX = mx - StepBadgeSize / 2;
            step.MapY = my - StepBadgeSize / 2;
            polylinePoints.Add(new Point(mx, my));
        }
        MapPolylinePoints = new ObservableCollection<Point>(polylinePoints);
    }

    /// <summary>Přiblížení na jeden monitor: mapa zobrazí jen ten obdélník ve výchozí (pevné) velikosti mapy,
    /// ostatní se schovají (dřív zůstávaly vykreslené i po zaostření, což bylo matoucí).</summary>
    private static Func<double, double, (double X, double Y)> BuildFocusedMap(
        MonitorBounds monitor, int index, double mapWidth, double mapHeight, List<MapMonitorRectViewModel> rects)
    {
        double scale = Math.Min(mapWidth / monitor.Width, mapHeight / monitor.Height);
        double offsetX = (mapWidth - monitor.Width * scale) / 2;
        double offsetY = (mapHeight - monitor.Height * scale) / 2;

        rects.Add(new MapMonitorRectViewModel
        {
            Index = index,
            X = offsetX,
            Y = offsetY,
            Width = monitor.Width * scale,
            Height = monitor.Height * scale,
            IsFocused = true
        });

        return (worldX, worldY) => (offsetX + (worldX - monitor.X) * scale, offsetY + (worldY - monitor.Y) * scale);
    }

    /// <summary>Přehled všech monitorů: seskupí monitory do řádků podle skutečného svislého překryvu (monitory
    /// nad sebou tak skončí v různých řádcích, monitory vedle sebe ve stejném), v řádku je vykreslí zleva
    /// doprava s mezerou místo na skutečných (často se překrývajících nebo těsně na sebe navazujících)
    /// souřadnicích virtuální plochy. Mapa se roztáhne do šířky/výšky podle počtu monitorů a řádků (do
    /// MaxOverviewRowWidth, pak zalomí další řádek). Body sekvence se namapují na monitor, do kterého
    /// ve skutečnosti spadají.</summary>
    private Func<double, double, (double X, double Y)> BuildOverviewMap(
        IReadOnlyList<MonitorBounds> monitors, List<MapMonitorRectViewModel> rects, out double canvasWidth, out double canvasHeight)
    {
        var rows = GroupIntoRows(monitors);

        double maxMonitorHeight = monitors.Max(m => (double)m.Height);
        double availableRowHeight = Math.Max(1, DefaultMapHeight / Math.Max(1, Math.Min(rows.Count, 2)) - MonitorMapPaddingPx);
        double heightScale = availableRowHeight / maxMonitorHeight;

        double widthScale = double.MaxValue;
        foreach (var row in rows)
        {
            double sumWidth = row.Sum(t => (double)t.Monitor.Width);
            double availableWidth = Math.Max(1, MaxOverviewRowWidth - 2 * MonitorMapPaddingPx - (row.Count - 1) * MonitorGapPx);
            widthScale = Math.Min(widthScale, availableWidth / Math.Max(1, sumWidth));
        }

        double scale = Math.Min(heightScale, widthScale);

        // 1. průchod: spočítat rozměr každého řádku při daném měřítku, abychom znali šířku/výšku plátna.
        var rowWidths = rows.Select(row => row.Sum(t => t.Monitor.Width * scale) + (row.Count - 1) * MonitorGapPx).ToList();
        var rowHeights = rows.Select(row => row.Max(t => (double)t.Monitor.Height) * scale).ToList();

        canvasWidth = Math.Max(DefaultMapWidth, rowWidths.Max() + 2 * MonitorMapPaddingPx);
        canvasHeight = Math.Max(DefaultMapHeight, rowHeights.Sum() + (rows.Count - 1) * MonitorRowGapPx + 2 * MonitorMapPaddingPx);

        // 2. průchod: umístit monitory, každý řádek vodorovně vycentrovaný v canvasWidth a celý blok řádků
        // svisle vycentrovaný v canvasHeight (typicky jeden řádek monitorů vedle sebe pěkně uprostřed mapy,
        // ne přilepený nahoře), pokud pro daný monitor existuje ruční pozice (uživatel ho přetáhl), použije se ta.
        double contentHeight = rowHeights.Sum() + (rows.Count - 1) * MonitorRowGapPx;
        var placed = new List<(MonitorBounds Monitor, int Index, double X, double Y, double Width, double Height)>();
        double cursorY = (canvasHeight - contentHeight) / 2;
        for (int r = 0; r < rows.Count; r++)
        {
            double cursorX = (canvasWidth - rowWidths[r]) / 2;
            foreach (var (monitor, originalIndex) in rows[r])
            {
                double w = monitor.Width * scale;
                double h = monitor.Height * scale;
                double x = cursorX;
                double y = cursorY + (rowHeights[r] - h) / 2;

                if (_manualMonitorPositions.TryGetValue(originalIndex, out var manual))
                {
                    x = manual.X;
                    y = manual.Y;
                }

                rects.Add(new MapMonitorRectViewModel { Index = originalIndex, X = x, Y = y, Width = w, Height = h, IsFocused = false });
                placed.Add((monitor, originalIndex, x, y, w, h));
                cursorX += w + MonitorGapPx;
            }
            cursorY += rowHeights[r] + MonitorRowGapPx;
        }

        return (worldX, worldY) =>
        {
            var target = FindContainingOrNearest(placed, worldX, worldY);
            double relX = Math.Clamp((worldX - target.Monitor.X) / Math.Max(1, target.Monitor.Width), 0, 1);
            double relY = Math.Clamp((worldY - target.Monitor.Y) / Math.Max(1, target.Monitor.Height), 0, 1);
            return (target.X + relX * target.Width, target.Y + relY * target.Height);
        };
    }

    /// <summary>Seřadí monitory podle Y a rozdělí je do řádků: monitor patří do posledního otevřeného řádku,
    /// pokud se jeho svislý rozsah s tím řádkem alespoň částečně překrývá (typicky monitory vedle sebe se
    /// stejnou výškou), jinak založí řádek nový (monitor posunutý nad/pod ostatní).</summary>
    private static List<List<(MonitorBounds Monitor, int Index)>> GroupIntoRows(IReadOnlyList<MonitorBounds> monitors)
    {
        var byY = monitors
            .Select((monitor, index) => (Monitor: monitor, Index: index))
            .OrderBy(t => t.Monitor.Y)
            .ToList();

        var rows = new List<List<(MonitorBounds Monitor, int Index)>>();
        foreach (var item in byY)
        {
            var currentRow = rows.Count > 0 ? rows[^1] : null;
            bool overlapsCurrentRow = currentRow is not null && currentRow.Any(existing =>
                RangesOverlap(existing.Monitor.Y, existing.Monitor.Y + existing.Monitor.Height, item.Monitor.Y, item.Monitor.Y + item.Monitor.Height));

            if (overlapsCurrentRow) currentRow!.Add(item);
            else rows.Add(new List<(MonitorBounds, int)> { item });
        }

        foreach (var row in rows) row.Sort((a, b) => a.Monitor.X.CompareTo(b.Monitor.X));
        return rows;
    }

    private static bool RangesOverlap(double aStart, double aEnd, double bStart, double bEnd) =>
        aStart < bEnd && bStart < aEnd;

    private static (MonitorBounds Monitor, int Index, double X, double Y, double Width, double Height) FindContainingOrNearest(
        List<(MonitorBounds Monitor, int Index, double X, double Y, double Width, double Height)> placed, double worldX, double worldY)
    {
        foreach (var candidate in placed)
        {
            if (worldX >= candidate.Monitor.X && worldX < candidate.Monitor.X + candidate.Monitor.Width &&
                worldY >= candidate.Monitor.Y && worldY < candidate.Monitor.Y + candidate.Monitor.Height)
            {
                return candidate;
            }
        }

        return placed.OrderBy(candidate => DistanceSquaredToMonitor(worldX, worldY, candidate.Monitor)).First();
    }

    private static double DistanceSquaredToMonitor(double x, double y, MonitorBounds monitor)
    {
        double dx = Math.Max(0, Math.Max(monitor.X - x, x - (monitor.X + monitor.Width)));
        double dy = Math.Max(0, Math.Max(monitor.Y - y, y - (monitor.Y + monitor.Height)));
        return dx * dx + dy * dy;
    }

    private static string FormatHotkey(HotkeyConfig hotkey)
    {
        var parts = hotkey.Modifiers.Select(m => m.ToString()).Append(hotkey.MainKey.ToString());
        return string.Join("+", parts);
    }
}
