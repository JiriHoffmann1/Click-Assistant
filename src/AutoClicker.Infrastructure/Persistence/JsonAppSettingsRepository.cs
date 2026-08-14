using System.Text.Json;
using AutoClicker.Core.Models;
using AutoClicker.Core.Persistence;

namespace AutoClicker.Infrastructure.Persistence;

public sealed class JsonAppSettingsRepository : IAppSettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonAppSettingsRepository()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoClicker", "settings.json"))
    {
    }

    public JsonAppSettingsRepository(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new AppSettings();

        try
        {
            // ConfigureAwait(false): MainWindow načítá nastavení synchronně (GetAwaiter().GetResult())
            // ještě před InitializeComponent(), aby byl jazyk hotový před prvním vyhodnocením XAML
            // bindingů. Bez ConfigureAwait(false) by se pokračování po awaitu snažilo vrátit zpět na
            // UI vlákno přes jeho SynchronizationContext - jenže to UI vlákno je právě zablokované
            // čekáním na tenhle Task, takže by šlo o klasický "sync-over-async" deadlock.
            using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions).ConfigureAwait(false);
            return settings ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Poškozený soubor nastavení - vrátit výchozí hodnoty místo pádu appky při startu.
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var tempPath = _filePath + ".tmp";

        using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
