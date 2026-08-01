using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using AutoClicker.App.Views;
using AutoClicker.Core.Engine;
using AutoClicker.Core.Models;
using AutoClicker.Core.Persistence;
using AutoClicker.Core.Screen;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoClicker.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IProfileRepository _profileRepository;
    private readonly ClickSequenceExecutor _executor;
    private readonly IScreenInfoProvider _screenInfoProvider;

    public Window? OwnerWindow { get; set; }

    public ObservableCollection<ClickProfile> Profiles { get; } = new();

    public ProfileEditorViewModel Editor { get; }

    [ObservableProperty]
    private ClickProfile? _selectedProfile;

    [ObservableProperty]
    private string _statusText = "Nečinný";

    [ObservableProperty]
    private bool _isRunning;

    public MainWindowViewModel(
        IProfileRepository profileRepository,
        ClickSequenceExecutor executor,
        IGlobalInputListener globalListener,
        IScreenInfoProvider screenInfoProvider)
    {
        _profileRepository = profileRepository;
        _executor = executor;
        _screenInfoProvider = screenInfoProvider;
        _executor.StatusChanged += OnStatusChanged;

        Editor = new ProfileEditorViewModel(globalListener, screenInfoProvider);
    }

    public async Task InitializeAsync()
    {
        var loaded = await _profileRepository.LoadAllAsync();
        Profiles.Clear();
        foreach (var profile in loaded) Profiles.Add(profile);

        if (Profiles.Count > 0) SelectedProfile = Profiles[0];
        else NewProfile();
    }

    partial void OnSelectedProfileChanged(ClickProfile? value)
    {
        if (value is not null) Editor.LoadFrom(value);
    }

    [RelayCommand]
    private void NewProfile()
    {
        Editor.ResetToNewProfile();
        SelectedProfile = null;
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        var profile = Editor.ToClickProfile();
        await _profileRepository.SaveAsync(profile);

        var existingIndex = IndexOfProfile(profile.Id);
        if (existingIndex >= 0) Profiles[existingIndex] = profile;
        else Profiles.Add(profile);

        SelectedProfile = profile;
        StatusText = "Profil uložen";
    }

    [RelayCommand]
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null) return;

        await _profileRepository.DeleteAsync(SelectedProfile.Id);
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.Count > 0 ? Profiles[0] : null;
        if (SelectedProfile is null) NewProfile();
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning || Editor.Steps.Count == 0) return;

        var currentSnapshot = _screenInfoProvider.GetCurrentSnapshot();
        var profile = Editor.ToClickProfile(currentSnapshot);

        if (profile.CapturedScreenSnapshot is { } captured && !captured.IsCompatibleWith(currentSnapshot))
        {
            var choice = await ShowResolutionMismatchDialogAsync(captured, currentSnapshot);
            switch (choice)
            {
                case ResolutionMismatchChoice.Rescale:
                    profile = ProfileRescaler.Rescale(profile, captured, currentSnapshot);
                    break;
                case ResolutionMismatchChoice.ContinueAnyway:
                    break;
                default:
                    return;
            }
        }

        await _executor.StartAsync(profile);
    }

    [RelayCommand]
    private void Stop() => _executor.Stop();

    private Task<ResolutionMismatchChoice> ShowResolutionMismatchDialogAsync(ScreenSnapshot from, ScreenSnapshot to)
    {
        if (OwnerWindow is null) return Task.FromResult(ResolutionMismatchChoice.Cancel);

        var dialog = new ResolutionMismatchDialog(from.Monitors.Count, to.Monitors.Count);
        return dialog.ShowDialog<ResolutionMismatchChoice>(OwnerWindow);
    }

    private int IndexOfProfile(Guid id)
    {
        for (int i = 0; i < Profiles.Count; i++)
            if (Profiles[i].Id == id) return i;
        return -1;
    }

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
