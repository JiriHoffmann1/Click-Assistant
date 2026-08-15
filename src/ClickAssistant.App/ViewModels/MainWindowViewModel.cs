using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using ClickAssistant.App.Localization;
using ClickAssistant.App.Views;
using ClickAssistant.Core.Engine;
using ClickAssistant.Core.Models;
using ClickAssistant.Core.Persistence;
using ClickAssistant.Core.Screen;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickAssistant.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IProfileRepository _profileRepository;
    private readonly ClickSequenceExecutor _executor;
    private readonly IGlobalInputListener _globalListener;
    private readonly IScreenInfoProvider _screenInfoProvider;
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly object _startHotkeySubscriberId = new();
    private readonly object _stopHotkeySubscriberId = new();
    private ClickProfile? _lastStartedProfile;
    

    public Window? OwnerWindow { get; set; }

    public ObservableCollection<ClickProfile> Profiles { get; } = new();

    public ProfileEditorViewModel Editor { get; }

    public IReadOnlyList<LocalizationManager.LanguageOption> AvailableLanguages => LocalizationManager.Instance.AvailableLanguages;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new("Auto", LocalizationManager.Instance["theme.auto"]),
        new("Light", LocalizationManager.Instance["theme.light"]),
        new("Dark", LocalizationManager.Instance["theme.dark"])
    ];

    [ObservableProperty]
    private ClickProfile? _selectedProfile;

    [ObservableProperty]
    private string _statusText = LocalizationManager.Instance["status.idle"];

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _canStart;

    [ObservableProperty]
    private LocalizationManager.LanguageOption _selectedLanguage;

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    public MainWindowViewModel(
        IProfileRepository profileRepository,
        ClickSequenceExecutor executor,
        IGlobalInputListener globalListener,
        IScreenInfoProvider screenInfoProvider,
        IScreenCaptureProvider screenCaptureProvider,
        IMouseInfoProvider mouseInfoProvider,
        IAppSettingsRepository appSettingsRepository)
    {
        _profileRepository = profileRepository;
        _executor = executor;
        _globalListener = globalListener;
        _screenInfoProvider = screenInfoProvider;
        _appSettingsRepository = appSettingsRepository;
        _executor.StatusChanged += OnStatusChanged;
        _executor.ResolutionChangedDuringRun += OnResolutionChangedDuringRun;
        _globalListener.HotkeyPressed += OnGlobalHotkeyPressed;

        Editor = new ProfileEditorViewModel(globalListener, screenInfoProvider, screenCaptureProvider, mouseInfoProvider);
        Editor.HotkeyChanged += hotkey => _globalListener.RegisterHotkey(hotkey, _startHotkeySubscriberId);
        Editor.StopHotkeyChanged += hotkey =>
        {
            if (hotkey is { } config) _globalListener.RegisterHotkey(config, _stopHotkeySubscriberId);
            else _globalListener.UnregisterHotkey(_stopHotkeySubscriberId);
        };
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProfileEditorViewModel.HasStopHotkey)) UpdateCanStart();
        };
        UpdateCanStart();

        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == LocalizationManager.Instance.CurrentLanguage)
                             ?? AvailableLanguages[0];
        _selectedTheme = ThemeOptions.FirstOrDefault(t => t.Code == CurrentThemeCode()) ?? ThemeOptions[0];
    }

    private static string CurrentThemeCode()
    {
        var variant = Application.Current!.RequestedThemeVariant;
        if (variant == ThemeVariant.Light) return "Light";
        if (variant == ThemeVariant.Dark) return "Dark";
        return "Auto";
    }

    partial void OnSelectedLanguageChanged(LocalizationManager.LanguageOption value)
    {
        if (value.Code == LocalizationManager.Instance.CurrentLanguage) return;
        LocalizationManager.Instance.SetLanguage(value.Code);
        _ = _appSettingsRepository.SaveAsync(new AppSettings { Language = value.Code, Theme = SelectedTheme.Code });
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        Application.Current!.RequestedThemeVariant = value.Code switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        _ = _appSettingsRepository.SaveAsync(new AppSettings { Language = SelectedLanguage.Code, Theme = value.Code });
    }

    private void UpdateCanStart() => CanStart = !IsRunning && Editor.HasStopHotkey;

    partial void OnIsRunningChanged(bool value) => UpdateCanStart();

    public async Task InitializeAsync()
    {
        var loaded = await _profileRepository.LoadAllAsync();
        Profiles.Clear();
        foreach (var profile in loaded) Profiles.Add(profile);

        // Uživatel mohl začít definovat body ještě před dokončením tohoto načtení
        // (LoadAllAsync běží asynchronně po zobrazení okna) - v tom případě rozdělanou
        // práci v editoru nezahazovat výběrem/vytvořením jiného profilu.
        if (Editor.Steps.Count > 0) return;

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
        // NumericUpDown (Interval/Jitter/humanization fields) only pushes its typed text into the
        // bound Value when it loses focus, not per keystroke - clicking Save right after typing a
        // number can otherwise still be reading the pre-edit value, so the first click appears to
        // "not save" and only the second one (after focus has since moved) picks up the change.
        // Clearing focus here forces that pending commit before we snapshot the editor state.
        OwnerWindow?.FocusManager?.ClearFocus();

        var profile = Editor.ToClickProfile();
        await _profileRepository.SaveAsync(profile);

        var existingIndex = IndexOfProfile(profile.Id);
        if (existingIndex >= 0) Profiles[existingIndex] = profile;
        else Profiles.Add(profile);

        SelectedProfile = profile;
        StatusText = LocalizationManager.Instance["status.profileSaved"];
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

        // Same pending-NumericUpDown-commit reasoning as SaveProfileAsync above.
        OwnerWindow?.FocusManager?.ClearFocus();

        var currentSnapshot = _screenInfoProvider.GetCurrentSnapshot();
        var profile = Editor.ToClickProfile(currentSnapshot);

        if (profile.StopHotkey is null)
        {
            StatusText = LocalizationManager.Instance["status.needStopHotkey"];
            return;
        }

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

        _lastStartedProfile = profile;
        await _executor.StartAsync(profile);
    }

    [RelayCommand]
    private void Stop() => _executor.Stop();

    private void OnGlobalHotkeyPressed(object? sender, GlobalHotkeyEventArgs e)
    {
        if (Equals(e.SubscriberId, _startHotkeySubscriberId))
        {
            Dispatcher.UIThread.Post(() => StartCommand.Execute(null));
        }
        else if (Equals(e.SubscriberId, _stopHotkeySubscriberId))
        {
            Dispatcher.UIThread.Post(() => StopCommand.Execute(null));
        }
    }

    private void OnResolutionChangedDuringRun(object? sender, ResolutionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            StatusText = LocalizationManager.Instance["status.stoppedResolutionChanged"];

            var choice = await ShowResolutionMismatchDialogAsync(e.Previous, e.Current);
            if (choice == ResolutionMismatchChoice.Cancel || _lastStartedProfile is null) return;

            var profile = choice == ResolutionMismatchChoice.Rescale
                ? ProfileRescaler.Rescale(_lastStartedProfile, e.Previous, e.Current)
                : _lastStartedProfile with { CapturedScreenSnapshot = e.Current };

            _lastStartedProfile = profile;
            await _executor.StartAsync(profile);
        });
    }

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
            if (e.Status == EngineStatus.Running) StatusText = LocalizationManager.Instance["status.running"];
            else if (e.Status == EngineStatus.Stopped && StatusText != LocalizationManager.Instance["status.stoppedResolutionChanged"])
                StatusText = LocalizationManager.Instance["status.stopped"];
        });
    }
}
