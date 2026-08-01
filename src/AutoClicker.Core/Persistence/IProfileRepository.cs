using AutoClicker.Core.Models;

namespace AutoClicker.Core.Persistence;

public interface IProfileRepository
{
    Task<IReadOnlyList<ClickProfile>> LoadAllAsync();
    Task SaveAsync(ClickProfile profile);
    Task DeleteAsync(Guid profileId);
}
