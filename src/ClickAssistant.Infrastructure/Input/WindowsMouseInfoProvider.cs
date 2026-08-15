using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClickAssistant.Core.Engine;

namespace ClickAssistant.Infrastructure.Input;

/// <summary>
/// Zjišťuje počet tlačítek myši přes Win32 GetSystemMetrics(SM_CMOUSEBUTTONS) - stejná hodnota, jakou
/// hlásí Ovládací panely Windows. Funguje jen na Windows a jen pro standardní tlačítka rozpoznaná ovladačem
/// myši (herní myši s dalšími tlačítky navázanými přes vlastní software appka takto nerozpozná).
/// </summary>
public sealed class WindowsMouseInfoProvider : IMouseInfoProvider
{
    private const int SM_CMOUSEBUTTONS = 43;
    private const int MinButtons = 3;
    private const int MaxButtons = 5;

    public int GetButtonCount()
    {
        if (!OperatingSystem.IsWindows()) return MinButtons;
        return GetButtonCountOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static int GetButtonCountOnWindows()
    {
        try
        {
            int count = GetSystemMetrics(SM_CMOUSEBUTTONS);
            return Math.Clamp(count <= 0 ? MinButtons : count, MinButtons, MaxButtons);
        }
        catch
        {
            // GetSystemMetrics prakticky neselhává, ale pro jistotu ať appka místo pádu jen nabídne základní tři tlačítka.
            return MinButtons;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
