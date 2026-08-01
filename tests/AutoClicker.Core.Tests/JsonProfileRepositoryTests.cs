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

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }
}
