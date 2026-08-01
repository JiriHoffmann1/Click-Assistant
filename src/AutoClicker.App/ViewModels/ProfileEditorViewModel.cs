using System.Collections.ObjectModel;
using Avalonia.Threading;
using AutoClicker.Core.Engine;
using AutoClicker.Core.Models;
using AutoClicker.Core.Screen;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoClicker.App.ViewModels;

public partial class ProfileEditorViewModel : ObservableObject
{
    private readonly IGlobalInputListener _globalListener;
    private readonly IScreenInfoProvider _screenInfoProvider;
    private IDisposable? _activeCapture;

    public Guid ProfileId { get; private set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "Nový profil";

    [ObservableProperty]
    private SequenceOrderMode _orderMode = SequenceOrderMode.Sequential;

    [ObservableProperty]
    private int _baseIntervalMs = 500;

    [ObservableProperty]
    private int _jitterMs = 50;

    [ObservableProperty]
    private RepeatMode _repeat = RepeatMode.Infinite;

    [ObservableProperty]
    private int _repeatCount = 1;

    [ObservableProperty]
    private bool _isCapturingPoint;

    [ObservableProperty]
    private SequenceStepViewModel? _selectedStep;

    public ObservableCollection<SequenceStepViewModel> Steps { get; } = new();

    public ScreenSnapshot? CapturedScreenSnapshot { get; private set; }

    public IReadOnlyList<SequenceOrderMode> OrderModeValues { get; } = Enum.GetValues<SequenceOrderMode>();
    public IReadOnlyList<RepeatMode> RepeatModeValues { get; } = Enum.GetValues<RepeatMode>();

    public ProfileEditorViewModel(IGlobalInputListener globalListener, IScreenInfoProvider screenInfoProvider)
    {
        _globalListener = globalListener;
        _screenInfoProvider = screenInfoProvider;
    }

    public void LoadFrom(ClickProfile profile)
    {
        ProfileId = profile.Id;
        Name = profile.Name;
        OrderMode = profile.OrderMode;
        BaseIntervalMs = profile.Timing.BaseIntervalMs;
        JitterMs = profile.Timing.JitterMs;
        Repeat = profile.Timing.Repeat;
        RepeatCount = profile.Timing.RepeatCount;
        CapturedScreenSnapshot = profile.CapturedScreenSnapshot;

        Steps.Clear();
        var order = profile.CustomOrder is { Count: > 0 }
            ? profile.CustomOrder.Select(id => profile.Points.FirstOrDefault(p => p.Id == id)).Where(p => p is not null).Select(p => p!)
            : profile.Points;
        foreach (var point in order) Steps.Add(new SequenceStepViewModel(point));
        RenumberSteps();
    }

    public void ResetToNewProfile()
    {
        ProfileId = Guid.NewGuid();
        Name = "Nový profil";
        OrderMode = SequenceOrderMode.Sequential;
        BaseIntervalMs = 500;
        JitterMs = 50;
        Repeat = RepeatMode.Infinite;
        RepeatCount = 1;
        CapturedScreenSnapshot = null;
        Steps.Clear();
        SelectedStep = null;
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
            BaseIntervalMs = BaseIntervalMs,
            JitterMs = JitterMs,
            Repeat = Repeat,
            RepeatCount = RepeatCount
        },
        CapturedScreenSnapshot = currentSnapshot ?? CapturedScreenSnapshot
    };

    [RelayCommand]
    private void AddPoint()
    {
        if (IsCapturingPoint) return;

        IsCapturingPoint = true;
        _activeCapture = _globalListener.CaptureNextClick(point =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var step = new SequenceStepViewModel(new ClickPoint
                {
                    Name = $"Bod {Steps.Count + 1}",
                    Location = point
                });
                Steps.Add(step);
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
    private void RemoveStep(SequenceStepViewModel? step)
    {
        step ??= SelectedStep;
        if (step is null) return;
        Steps.Remove(step);
        if (SelectedStep == step) SelectedStep = null;
        RenumberSteps();
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
    }
}
