using System.Threading;
using AutoClicker.Core.Models;
using AutoClicker.Infrastructure.Persistence;
using Xunit;

namespace AutoClicker.Core.Tests;

public class JsonAppSettingsRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "AutoClickerSettingsTests_" + Guid.NewGuid());
    private string FilePath => Path.Combine(_tempDir, "settings.json");

    [Fact]
    public async Task LoadAsync_WhenFileMissing_ReturnsDefaultSettings()
    {
        var repository = new JsonAppSettingsRepository(FilePath);

        var settings = await repository.LoadAsync();

        Assert.Equal("cs", settings.Language);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsLanguage()
    {
        var repository = new JsonAppSettingsRepository(FilePath);

        await repository.SaveAsync(new AppSettings { Language = "en" });
        var loaded = await repository.LoadAsync();

        Assert.Equal("en", loaded.Language);
    }

    [Fact]
    public async Task SaveAsync_Twice_OverwritesPreviousValue()
    {
        var repository = new JsonAppSettingsRepository(FilePath);

        await repository.SaveAsync(new AppSettings { Language = "en" });
        await repository.SaveAsync(new AppSettings { Language = "ja" });
        var loaded = await repository.LoadAsync();

        Assert.Equal("ja", loaded.Language);
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_ReturnsDefaultSettingsInsteadOfThrowing()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(FilePath, "{ not valid json");
        var repository = new JsonAppSettingsRepository(FilePath);

        var settings = await repository.LoadAsync();

        Assert.Equal("cs", settings.Language);
    }

    [Fact]
    public void LoadAsync_BlockingCallOnCapturedSynchronizationContext_DoesNotDeadlock()
    {
        // Regresní test na sync-over-async deadlock: MainWindow volá
        // settingsRepository.LoadAsync().GetAwaiter().GetResult() synchronně na UI vlákně, ještě
        // před InitializeComponent(). Pokud by repository interně neudělalo ConfigureAwait(false),
        // pokračování po 'await' by se pokusilo vrátit na tenhle stejný (zablokovaný) SynchronizationContext
        // a test by zamrzl navždy - proto běží s vlastním timeoutem místo prostého volání.
        var repository = new JsonAppSettingsRepository(FilePath);
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new SingleThreadedTestSynchronizationContext());

#pragma warning disable xUnit1031 // Záměrně blokující volání - přesně to, co test ověřuje (viz komentář výše).
            var completed = Task.Run(() => repository.LoadAsync().GetAwaiter().GetResult())
                .Wait(TimeSpan.FromSeconds(5));
#pragma warning restore xUnit1031

            Assert.True(completed, "LoadAsync() zamrzl při blokujícím volání na vlákně s vlastním SynchronizationContextem (sync-over-async deadlock).");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    /// <summary>
    /// Jednoduchý SynchronizationContext, který (na rozdíl od výchozího) nikdy sám nezpracuje frontu
    /// zaslaných pokračování - simuluje zablokované UI vlákno. Pokud by kód uvnitř repository nepoužil
    /// ConfigureAwait(false), Post() by se sice zavolal, ale nic by ho nikdy nevyzvedlo a Wait() výše
    /// by vypršel na timeoutu.
    /// </summary>
    private sealed class SingleThreadedTestSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Záměrně nic nedělá - simuluje kontext, který se nikdy nedostane k vyzvednutí pokračování.
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }
}
