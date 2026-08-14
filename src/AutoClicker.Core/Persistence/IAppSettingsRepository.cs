using AutoClicker.Core.Models;

namespace AutoClicker.Core.Persistence;

public interface IAppSettingsRepository
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}
