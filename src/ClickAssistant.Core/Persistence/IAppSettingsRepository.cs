using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Persistence;

public interface IAppSettingsRepository
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}
