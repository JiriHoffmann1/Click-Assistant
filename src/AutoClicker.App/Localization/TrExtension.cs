using System;
using Avalonia.Markup.Xaml;

namespace AutoClicker.App.Localization;

/// <summary>
/// XAML markup extension {loc:Tr key} - vrátí přeložený text pro daný klíč v aktuálně načteném jazyce.
/// Vrací obyčejný string (ne Binding), protože jazyk se nemění za běhu okna - viz LocalizationManager.
/// </summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => LocalizationManager.Instance[Key];
}
