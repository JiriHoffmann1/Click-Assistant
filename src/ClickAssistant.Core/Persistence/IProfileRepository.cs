using ClickAssistant.Core.Models;

namespace ClickAssistant.Core.Persistence;

public interface IProfileRepository
{
    Task<IReadOnlyList<ClickProfile>> LoadAllAsync();
    Task SaveAsync(ClickProfile profile);
    Task DeleteAsync(Guid profileId);
}
