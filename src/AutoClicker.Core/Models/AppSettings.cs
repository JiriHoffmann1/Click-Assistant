namespace AutoClicker.Core.Models;

public sealed record AppSettings
{
    public string Language { get; init; } = "cs";
}
