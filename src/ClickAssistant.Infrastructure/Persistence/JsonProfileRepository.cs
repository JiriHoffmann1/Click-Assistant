using System.Text.Json;
using System.Text.Json.Serialization;
using ClickAssistant.Core.Models;
using ClickAssistant.Core.Persistence;

namespace ClickAssistant.Infrastructure.Persistence;

public sealed class JsonProfileRepository : IProfileRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _profilesDirectory;

    public JsonProfileRepository()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickAssistant", "profiles"))
    {
    }

    public JsonProfileRepository(string profilesDirectory)
    {
        _profilesDirectory = profilesDirectory;
        Directory.CreateDirectory(_profilesDirectory);
    }

    public async Task<IReadOnlyList<ClickProfile>> LoadAllAsync()
    {
        var profiles = new List<ClickProfile>();
        foreach (var file in Directory.EnumerateFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var profile = await JsonSerializer.DeserializeAsync<ClickProfile>(stream, SerializerOptions);
                if (profile is not null) profiles.Add(profile);
            }
            catch (JsonException)
            {
                // Poškozený nebo ručně upravený soubor profilu - přeskočit, ať nezhatí načtení ostatních profilů.
            }
            catch (IOException)
            {
                // EnumerateFiles je líné - soubor mohl mezitím zmizet (souběžný DeleteAsync/SaveAsync
                // přejmenování). Stejně jako u poškozeného JSON: přeskočit, ne shodit celé načítání.
            }
        }
        return profiles;
    }

    public async Task SaveAsync(ClickProfile profile)
    {
        var finalPath = GetPath(profile.Id);
        // Unique per-call temp name (not just finalPath + ".tmp") so two saves of the same profile
        // racing (e.g. an autosave and an explicit Save landing at nearly the same moment) each get
        // their own file instead of throwing "file in use" on the second File.Create. They still only
        // race on the final overwrite-move, which is atomic - last writer wins, nothing throws.
        var tempPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, profile, SerializerOptions);
        }

        File.Move(tempPath, finalPath, overwrite: true);
    }

    public Task DeleteAsync(Guid profileId)
    {
        var path = GetPath(profileId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(Guid profileId) => Path.Combine(_profilesDirectory, $"{profileId}.json");
}
