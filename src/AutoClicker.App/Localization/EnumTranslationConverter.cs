using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AutoClicker.App.Localization;

/// <summary>
/// Převede hodnotu enumu (např. SequenceOrderMode.Random) na přeložený text hledáním klíče
/// "enum.{TypeName}.{Value}" (např. "enum.SequenceOrderMode.Random"). Použito v ComboBox.ItemTemplate,
/// aby uživatel viděl přeložené názvy voleb místo syrových jmen C# enumů.
/// </summary>
public sealed class EnumTranslationConverter : IValueConverter
{
    public static readonly EnumTranslationConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;
        var key = $"enum.{value.GetType().Name}.{value}";
        return LocalizationManager.Instance[key];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
