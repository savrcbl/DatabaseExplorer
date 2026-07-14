using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

/// <summary>
/// Resolves the registered <see cref="IDatabaseProvider"/> implementations. This is the
/// single lookup point the rest of the application uses, so new engines only need to be
/// registered with the DI container to become available everywhere.
/// </summary>
public interface IDatabaseProviderFactory
{
    /// <summary>Returns every registered provider, for populating the provider selector.</summary>
    IReadOnlyList<IDatabaseProvider> GetAllProviders();

    /// <summary>Returns the provider registered for <paramref name="type"/>.</summary>
    /// <exception cref="InvalidOperationException">No provider is registered for the type.</exception>
    IDatabaseProvider GetProvider(DatabaseProviderType type);
}
