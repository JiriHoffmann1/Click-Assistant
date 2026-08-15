using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ClickAssistant.App.Localization;

/// <summary>Formátuje číslo kroku pomocí přeloženého vzoru "step.number" (např. "Krok {0}" / "Step {0}").</summary>
public sealed class StepNumberConverter : IValueConverter
{
    public static readonly StepNumberConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Format(culture, LocalizationManager.Instance["step.number"], value);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
