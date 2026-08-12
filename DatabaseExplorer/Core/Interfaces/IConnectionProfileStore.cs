using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

public interface IConnectionProfileStore
{
    Task<IReadOnlyList<SavedConnectionProfile>> LoadAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SavedConnectionProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
