using System.ComponentModel;

namespace ClickAssistant.App.Localization;

/// <summary>
/// Jeden proxy objekt na binding vytvořený {loc:Tr key}. Naslouchá LocalizationManager.PropertyChanged
/// a při změně jazyka přepošle PropertyChanged na Value, takže se binding v XAML přebinduje živě.
/// </summary>
internal sealed class TrProxy : INotifyPropertyChanged
{
    private readonly string _key;

    public TrProxy(string key)
    {
        _key = key;
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;
    }

    public string Value => LocalizationManager.Instance[_key];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
