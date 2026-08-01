using AutoClicker.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoClicker.App.ViewModels;

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
    private MouseButtonType _button;

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

    public SequenceStepViewModel(ClickPoint point)
    {
        Id = point.Id;
        _name = point.Name;
        _x = point.Location.X;
        _y = point.Location.Y;
        _button = point.Button;
        _clickCount = point.ClickCount;
        _useCustomDelay = point.DelayAfterMsOverride.HasValue;
        _delayAfterMs = point.DelayAfterMsOverride ?? 500;
    }

    public ClickPoint ToClickPoint() => new()
    {
        Id = Id,
        Name = Name,
        Location = new ScreenPoint((int)X, (int)Y),
        Button = Button,
        ClickCount = Math.Max(1, (int)ClickCount),
        DelayAfterMsOverride = UseCustomDelay ? (int)DelayAfterMs : null
    };
}
