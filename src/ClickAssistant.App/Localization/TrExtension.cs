using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ClickAssistant.App.Localization;

/// <summary>
/// XAML markup extension {loc:Tr key} - vrátí přeložený text pro daný klíč v aktuálně načteném jazyce.
/// Vrací Binding na TrProxy místo obyčejného stringu, aby se text živě přebindoval při přepnutí jazyka.
/// </summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(TrProxy.Value)) { Source = new TrProxy(Key), Mode = BindingMode.OneWay };
}
