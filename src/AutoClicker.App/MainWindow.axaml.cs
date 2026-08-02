using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using AutoClicker.App.Services;
using AutoClicker.App.ViewModels;
using AutoClicker.Core.Engine;
using AutoClicker.Infrastructure.Capture;
using AutoClicker.Infrastructure.Input;
using AutoClicker.Infrastructure.Persistence;

namespace AutoClicker.App;

public partial class MainWindow : Window
{
    private readonly SharpHookGlobalListener _globalListener = new();
    private TrayIcon? _trayIcon;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        var appIcon = new WindowIcon(AssetLoader.Open(new Uri("avares://AutoClicker.App/Assets/tray-icon.ico")));
        Icon = appIcon;

        var screenInfoProvider = new AvaloniaScreenInfoProvider(this);
        var executor = new ClickSequenceExecutor(new SharpHookInputSimulator(), screenInfoProvider: screenInfoProvider);
        var viewModel = new MainWindowViewModel(
            new JsonProfileRepository(),
            executor,
            _globalListener,
            screenInfoProvider,
            new WindowsScreenCaptureProvider())
        {
            OwnerWindow = this
        };

        DataContext = viewModel;

        SetupTrayIcon(viewModel, appIcon);

        _globalListener.Start();
        Opened += async (_, _) => await viewModel.InitializeAsync();
        Closing += OnWindowClosing;
        Closed += (_, _) => _globalListener.Stop();
    }

    private void SetupTrayIcon(MainWindowViewModel viewModel, WindowIcon icon)
    {
        var showItem = new NativeMenuItem("Zobrazit");
        showItem.Click += (_, _) => ShowMainWindow();

        var startItem = new NativeMenuItem("Start");
        startItem.Click += (_, _) => viewModel.StartCommand.Execute(null);

        var stopItem = new NativeMenuItem("Stop");
        stopItem.Click += (_, _) => viewModel.StopCommand.Execute(null);

        var exitItem = new NativeMenuItem("Konec");
        exitItem.Click += (_, _) =>
        {
            _allowClose = true;
            Close();
        };

        var menu = new NativeMenu { showItem, startItem, stopItem, new NativeMenuItemSeparator(), exitItem };

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "AutoClicker",
            Menu = menu
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(Avalonia.Application.Current!, new TrayIcons { _trayIcon });
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }
}
