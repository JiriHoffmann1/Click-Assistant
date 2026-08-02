namespace AutoClicker.Core.Screen;

public interface IScreenCaptureProvider
{
    /// <summary>Zachytí obdélníkovou oblast obrazovky (v souřadnicích celé virtuální plochy) a vrátí ji jako PNG. Null, pokud zachytávání není na této platformě podporováno.</summary>
    byte[]? CaptureRegion(int x, int y, int width, int height);
}
