namespace AutoClicker.App.ViewModels;

/// <summary>One entry of the Auto/Light/Dark picker. Code is persisted in AppSettings.Theme and mapped
/// to Avalonia's ThemeVariant; Label is the localized display text shown in the toolbar.</summary>
public sealed record ThemeOption(string Code, string Label);
