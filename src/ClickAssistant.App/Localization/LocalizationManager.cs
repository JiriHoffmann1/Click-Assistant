using System.ComponentModel;
using System.Text.Json;
using Avalonia.Platform;

namespace ClickAssistant.App.Localization;

/// <summary>
/// Drží aktuálně načtený slovník překladů a seznam podporovaných jazyků. Jazyk se nastavuje jednou při
/// startu appky (viz MainWindow konstruktor) - přepnutí za běhu appky se uloží do nastavení, ale projeví
/// se až po restartu, aby appka nemusela řešit živé přebindování všech textů v celém stromu UI.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    public sealed record LanguageOption(string Code, string NativeName, string Flag);

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new("cs", "Čeština", "🇨🇿"),
        new("en", "English", "🇬🇧"),
        new("zh", "中文", "🇨🇳"),
        new("hi", "हिन्दी", "🇮🇳"),
        new("es", "Español", "🇪🇸"),
        new("fr", "Français", "🇫🇷"),
        new("ar", "العربية", "🇸🇦"),
        new("bn", "বাংলা", "🇧🇩"),
        new("pt", "Português", "🇵🇹"),
        new("ru", "Русский", "🇷🇺"),
        new("ur", "اردو", "🇵🇰"),
        new("id", "Bahasa Indonesia", "🇮🇩"),
        new("de", "Deutsch", "🇩🇪"),
        new("ja", "日本語", "🇯🇵"),
        new("sw", "Kiswahili", "🇹🇿"),
        new("mr", "मराठी", "🇮🇳"),
        new("te", "తెలుగు", "🇮🇳"),
        new("tr", "Türkçe", "🇹🇷"),
        new("ta", "தமிழ்", "🇮🇳"),
        new("vi", "Tiếng Việt", "🇻🇳"),
        new("ko", "한국어", "🇰🇷"),
    ];

    private Dictionary<string, string> _strings = new();

    public string CurrentLanguage { get; private set; } = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager() => Load("en");

    /// <summary>Vrátí přeložený text pro daný klíč, nebo klíč samotný, pokud překlad chybí (nikdy nespadne).</summary>
    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    public void SetLanguage(string code)
    {
        if (code == CurrentLanguage) return;
        Load(code);
    }

    private void Load(string code)
    {
        var match = AvailableLanguages.FirstOrDefault(l => l.Code == code) ?? AvailableLanguages[0];
        CurrentLanguage = match.Code;
        _strings = LoadDictionary(CurrentLanguage);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    private static Dictionary<string, string> LoadDictionary(string code)
    {
        var uri = new Uri($"avares://ClickAssistant.App/Localization/Strings/{code}.json");
        using var stream = AssetLoader.Open(uri);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
    }
}
