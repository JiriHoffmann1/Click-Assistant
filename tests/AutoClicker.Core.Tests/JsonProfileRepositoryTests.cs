using AutoClicker.Core.Models;
using AutoClicker.Infrastructure.Persistence;
using Xunit;

namespace AutoClicker.Core.Tests;

public class JsonProfileRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "AutoClickerTests_" + Guid.NewGuid());
    private readonly JsonProfileRepository _repository;

    public JsonProfileRepositoryTests()
    {
        _repository = new JsonProfileRepository(_tempDir);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAll_RoundTripsProfile()
    {
        var profile = new ClickProfile
        {
            Name = "Test profil",
            Points =
            [
                new ClickPoint { Name = "Bod 1", Location = new ScreenPoint(10, 20), DelayAfterMsOverride = 250 }
            ],
            OrderMode = SequenceOrderMode.Random,
            Timing = new TimingConfig { BaseIntervalMs = 300, JitterMs = 20 }
        };

        await _repository.SaveAsync(profile);
        var loaded = await _repository.LoadAllAsync();

        var reloaded = Assert.Single(loaded);
        Assert.Equal(profile.Id, reloaded.Id);
        Assert.Equal(profile.Name, reloaded.Name);
        Assert.Equal(profile.OrderMode, reloaded.OrderMode);
        Assert.Equal(250, reloaded.Points[0].DelayAfterMsOverride);
        Assert.Equal(new ScreenPoint(10, 20), reloaded.Points[0].Location);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProfileFile()
    {
        var profile = new ClickProfile { Name = "Ke smazání" };
        await _repository.SaveAsync(profile);

        await _repository.DeleteAsync(profile.Id);
        var loaded = await _repository.LoadAllAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentProfile_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => _repository.DeleteAsync(Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task LoadAllAsync_OnEmptyDirectory_ReturnsEmptyList()
    {
        var loaded = await _repository.LoadAllAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task SaveAsync_SameIdTwice_OverwritesRatherThanDuplicating()
    {
        var profile = new ClickProfile { Name = "V1" };
        await _repository.SaveAsync(profile);

        var updated = profile with { Name = "V2" };
        await _repository.SaveAsync(updated);

        var loaded = await _repository.LoadAllAsync();
        var single = Assert.Single(loaded);
        Assert.Equal("V2", single.Name);
    }

    [Fact]
    public async Task LoadAllAsync_SkipsCorruptedJsonFile_AndStillLoadsValidOnes()
    {
        // Simuluje poškozený/ručně rozbitý soubor profilu (např. useknutý zápis po pádu appky).
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "corrupted.json"), "{ not valid json ]]]");

        var validProfile = new ClickProfile { Name = "V pořádku" };
        await _repository.SaveAsync(validProfile);

        var loaded = await _repository.LoadAllAsync();

        var single = Assert.Single(loaded);
        Assert.Equal("V pořádku", single.Name);
    }

    [Fact]
    public async Task LoadAllAsync_EmptyJsonFile_IsSkippedWithoutThrowing()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "empty.json"), string.Empty);

        var exception = await Record.ExceptionAsync(() => _repository.LoadAllAsync());

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_CreatesProfilesDirectoryIfMissing()
    {
        Assert.True(Directory.Exists(_tempDir));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }
}
