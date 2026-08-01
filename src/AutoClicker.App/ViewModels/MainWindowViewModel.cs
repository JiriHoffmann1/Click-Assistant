using Avalonia.Threading;
using AutoClicker.Core.Engine;
using AutoClicker.Core.Models;
using AutoClicker.Infrastructure.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoClicker.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ClickSequenceExecutor _executor;

    [ObservableProperty]
    private decimal _pointX = 500;

    [ObservableProperty]
    private decimal _pointY = 500;

    [ObservableProperty]
    private decimal _intervalMs = 500;

    [ObservableProperty]
    private string _statusText = "Nečinný";

    [ObservableProperty]
    private bool _isRunning;

    public MainWindowViewModel()
    {
        _executor = new ClickSequenceExecutor(new SharpHookInputSimulator());
        _executor.StatusChanged += OnStatusChanged;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning) return;

        var profile = new ClickProfile
        {
            Points = new List<ClickPoint>
            {
                new() { Name = "Bod 1", Location = new ScreenPoint((int)PointX, (int)PointY) }
            },
            Timing = new TimingConfig { BaseIntervalMs = (int)IntervalMs, Repeat = RepeatMode.Infinite }
        };

        await _executor.StartAsync(profile);
    }

    [RelayCommand]
    private void Stop() => _executor.Stop();

    private void OnStatusChanged(object? sender, EngineStatusEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsRunning = e.Status == EngineStatus.Running;
            StatusText = e.Status switch
            {
                EngineStatus.Running => "Běží",
                EngineStatus.Stopped => "Zastaveno",
                _ => "Nečinný"
            };
        });
    }
}
