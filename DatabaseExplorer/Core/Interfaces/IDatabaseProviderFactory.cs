using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

public interface IDatabaseProviderFactory
{
    IReadOnlyList<IDatabaseProvider> GetAllProviders();

    IDatabaseProvider GetProvider(DatabaseProviderType type);
}
