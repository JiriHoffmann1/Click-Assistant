using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using ClickAssistant.App.Localization;
using ClickAssistant.App.Services;
using ClickAssistant.App.ViewModels;
using ClickAssistant.Core.Engine;
using ClickAssistant.Infrastructure.Capture;
using ClickAssistant.Infrastructure.Input;
using ClickAssistant.Infrastructure.Persistence;

namespace ClickAssistant.App;

public partial class MainWindow : Window
{
    private readonly SharpHookGlobalListener _globalListener = new();
    private TrayIcon? _trayIcon;
    private bool _allowClose;

    public MainWindow()
    {
        MigrateLegacyAppDataFolder();

        // Jazyk se musí načíst před InitializeComponent(), aby XAML {loc:Tr ...} bindingy
        // naběhly rovnou se správným jazykem (přepnutí za běhu appky je pak živé, viz TrProxy).
        var settingsRepository = new JsonAppSettingsRepository();
        var appSettings = settingsRepository.LoadAsync().GetAwaiter().GetResult();
        LocalizationManager.Instance.SetLanguage(appSettings.Language);
        // Unlike language, theme changes apply live later (MainWindowViewModel.OnSelectedThemeChanged) -
        // this line only restores the saved choice at startup, before the first frame is painted.
        Application.Current!.RequestedThemeVariant = appSettings.Theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        InitializeComponent();

        var iconUri = new Uri("avares://ClickAssistant.App/Assets/tray-icon.ico");
        var appIcon = new WindowIcon(AssetLoader.Open(iconUri));
        Icon = appIcon;
        TitleBarIcon.Source = new Bitmap(AssetLoader.Open(iconUri));

        var screenInfoProvider = new AvaloniaScreenInfoProvider(this);
        var executor = new ClickSequenceExecutor(new SharpHookInputSimulator(), screenInfoProvider: screenInfoProvider);
        var viewModel = new MainWindowViewModel(
            new JsonProfileRepository(),
            executor,
            _globalListener,
            screenInfoProvider,
            new WindowsScreenCaptureProvider(),
            new WindowsMouseInfoProvider(),
            settingsRepository)
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
            ToolTipText = "Click Assistant",
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

    /// <summary>
    /// The custom brass title bar Border isn't a real OS title bar (ExtendClientAreaChromeHints
    /// leaves window dragging/maximize entirely to us for whatever area we draw), so it has to
    /// opt back into that behaviour by hand: left-drag moves the window, a double-click toggles
    /// maximize, matching what the native title bar would have done.
    /// </summary>
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            BeginMoveDrag(e);
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }

    /// <summary>
    /// Appka se přejmenovala z AutoClicker na Click Assistant, včetně %AppData% složky s profily/nastavením.
    /// Při prvním startu po přejmenování přesune starou AutoClicker složku na nové místo, ať uživatel
    /// nepřijde o dřív uložené profily. Neblokuje startup appky, pokud se migrace z nějakého důvodu nezdaří.
    /// </summary>
    private static void MigrateLegacyAppDataFolder()
    {
        try
        {
            var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var legacyDir = Path.Combine(appDataRoot, "AutoClicker");
            var currentDir = Path.Combine(appDataRoot, "ClickAssistant");

            if (Directory.Exists(legacyDir) && !Directory.Exists(currentDir))
            {
                Directory.Move(legacyDir, currentDir);
            }
        }
        catch (IOException)
        {
            // Migrace není kritická - appka si bez ní jen vytvoří prázdnou novou složku (viz JsonProfileRepository/JsonAppSettingsRepository).
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
