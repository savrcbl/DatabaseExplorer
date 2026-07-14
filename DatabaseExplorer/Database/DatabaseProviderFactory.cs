using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Database;

/// <summary>
/// Default <see cref="IDatabaseProviderFactory"/> implementation. Simply indexes whatever
/// <see cref="IDatabaseProvider"/> instances were registered with the DI container, so
/// supporting a new engine never requires touching this class.
/// </summary>
public sealed class DatabaseProviderFactory : IDatabaseProviderFactory
{
    private readonly IReadOnlyList<IDatabaseProvider> _providers;
    private readonly IReadOnlyDictionary<DatabaseProviderType, IDatabaseProvider> _byType;

    public DatabaseProviderFactory(IEnumerable<IDatabaseProvider> providers)
    {
        _providers = providers.ToList();
        _byType = _providers.ToDictionary(p => p.ProviderType);
    }

    public IReadOnlyList<IDatabaseProvider> GetAllProviders() => _providers;

    public IDatabaseProvider GetProvider(DatabaseProviderType type)
    {
        if (_byType.TryGetValue(type, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"No database provider is registered for '{type}'.");
    }
}
