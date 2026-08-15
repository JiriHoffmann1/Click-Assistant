using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using ClickAssistant.Core.Screen;

namespace ClickAssistant.Infrastructure.Capture;

/// <summary>
/// Zachytává oblast obrazovky přes GDI (System.Drawing.Graphics.CopyFromScreen). Funguje jen na Windows -
/// na jiných platformách CaptureRegion vrátí null (viz plán: reálný screenshot je Windows-first rozšíření).
/// </summary>
public sealed class WindowsScreenCaptureProvider : IScreenCaptureProvider
{
    public byte[]? CaptureRegion(int x, int y, int width, int height)
    {
        if (!OperatingSystem.IsWindows() || width <= 0 || height <= 0) return null;
        return CaptureOnWindows(x, y, width, height);
    }

    [SupportedOSPlatform("windows")]
    private static byte[]? CaptureOnWindows(int x, int y, int width, int height)
    {
        try
        {
            using var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch
        {
            // Zachytávání obrazovky může selhat (např. chybějící oprávnění, RDP relace bez GPU) -
            // v takovém případě appka jen nezobrazí náhled, neshodí se.
            return null;
        }
    }
}
