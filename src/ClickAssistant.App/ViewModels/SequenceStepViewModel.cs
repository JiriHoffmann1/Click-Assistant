using ClickAssistant.App.Localization;
using ClickAssistant.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClickAssistant.App.ViewModels;

public partial class SequenceStepViewModel : ObservableObject
{
    public Guid Id { get; }

    [ObservableProperty]
    private int _stepNumber;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private decimal _x;

    [ObservableProperty]
    private decimal _y;

    [ObservableProperty]
    private StepActionType _actionType;

    [ObservableProperty]
    private MouseButtonType _button;

    [ObservableProperty]
    private HookKeyCode? _key;

    [ObservableProperty]
    private string _keyDisplayText = LocalizationManager.Instance["hotkey.notSet"];

    [ObservableProperty]
    private bool _isCapturingKey;

    [ObservableProperty]
    private decimal _clickCount;

    [ObservableProperty]
    private bool _useCustomDelay;

    /// <summary>Platí jen když UseCustomDelay == true, jinak se použije globální interval z profilu.</summary>
    [ObservableProperty]
    private decimal _delayAfterMs;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Pozice tečky v souřadnicích prostorové mapy (SequenceMapView), přepočítává ProfileEditorViewModel.</summary>
    [ObservableProperty]
    private double _mapX;

    [ObservableProperty]
    private double _mapY;

    public IReadOnlyList<StepActionType> ActionTypeValues { get; } = Enum.GetValues<StepActionType>();

    /// <summary>Nabídka dostupných tlačítek myši - omezuje ji ProfileEditorViewModel podle skutečně
    /// zjištěného počtu tlačítek připojené myši (viz RefreshMouseButtonOptions), default je bezpečné minimum.</summary>
    [ObservableProperty]
    private IReadOnlyList<MouseButtonType> _mouseButtonValues = new[] { MouseButtonType.Left, MouseButtonType.Right, MouseButtonType.Middle };

    /// <summary>Souřadnice X/Y se zadávají pro klik myší i pro stisk klávesy na pozici (ta taky nejdřív
    /// přesune kurzor) - jen u čistého stisku klávesy bez pohybu nemají smysl.</summary>
    public bool ShowPositionFields => ActionType is StepActionType.MouseClick or StepActionType.KeyPressAtPosition;
    public bool ShowButtonField => ActionType == StepActionType.MouseClick;
    public bool ShowKeyField => ActionType is StepActionType.KeyPress or StepActionType.KeyPressAtPosition;

    partial void OnActionTypeChanged(StepActionType value)
    {
        OnPropertyChanged(nameof(ShowPositionFields));
        OnPropertyChanged(nameof(ShowButtonField));
        OnPropertyChanged(nameof(ShowKeyField));
    }

    public SequenceStepViewModel(ClickPoint point)
    {
        Id = point.Id;
        _name = point.Name;
        _x = point.Location.X;
        _y = point.Location.Y;
        _actionType = point.ActionType;
        _button = point.Button;
        _key = point.Key;
        _keyDisplayText = point.Key is { } key ? key.ToString() : LocalizationManager.Instance["hotkey.notSet"];
        _clickCount = point.ClickCount;
        _useCustomDelay = point.DelayAfterMsOverride.HasValue;
        _delayAfterMs = point.DelayAfterMsOverride ?? 500;
    }

    public ClickPoint ToClickPoint() => new()
    {
        Id = Id,
        Name = Name,
        Location = new ScreenPoint((int)X, (int)Y),
        ActionType = ActionType,
        Button = Button,
        Key = Key,
        ClickCount = Math.Max(1, (int)ClickCount),
        DelayAfterMsOverride = UseCustomDelay ? (int)DelayAfterMs : null
    };
}
