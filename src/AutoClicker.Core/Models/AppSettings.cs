namespace AutoClicker.Core.Models;

public sealed record AppSettings
{
    public string Language { get; init; } = "en";

    /// <summary>"Auto" (follow OS), "Light", or "Dark". "Auto" maps to Avalonia's ThemeVariant.Default.</summary>
    public string Theme { get; init; } = "Auto";
}
