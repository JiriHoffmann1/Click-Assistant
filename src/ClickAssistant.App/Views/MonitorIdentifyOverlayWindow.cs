using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ClickAssistant.App.Views;

/// <summary>Fullscreen (přes celý fyzický monitor) borderless overlay okno pro funkci "Identify" v mapě
/// bodů - ukáže velké číslo monitoru jako menší odznak uprostřed, obdoba identifikace displejů ve Windows.
/// Zbytek okna je průhledný a klikatelný skrz (WS_EX_TRANSPARENT), aby uživatel mohl appku pod overlayem
/// dál ovládat - typicky znovu kliknout na "Identify" a identifikaci vypnout. Vytváří a zavírá ho
/// SequenceMapView podle ProfileEditorViewModel.IsIdentifyingMonitors.</summary>
public sealed class MonitorIdentifyOverlayWindow : Window
{
    public MonitorIdentifyOverlayWindow(int number)
    {
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        ShowActivated = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };

        Content = new Border
        {
            Width = 200,
            Height = 200,
            CornerRadius = new CornerRadius(100),
            Background = new SolidColorBrush(Color.FromArgb(215, 30, 30, 40)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontSize = 100,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows()) MakeClickThroughOnWindows();
        };
    }

    [SupportedOSPlatform("windows")]
    private void MakeClickThroughOnWindows()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        int exStyle = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, exStyle | WsExTransparent | WsExLayered);
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
