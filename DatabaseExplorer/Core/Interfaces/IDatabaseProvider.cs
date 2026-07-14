using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

/// <summary>
/// Describes a supported database engine and acts as a factory for connections to it.
/// To add support for a new engine, implement this interface (plus
/// <see cref="IDatabaseConnection"/> and <see cref="IDatabaseQueryService"/>) and register
/// it in the dependency injection container — no existing code needs to change.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>The engine this provider represents.</summary>
    DatabaseProviderType ProviderType { get; }

    /// <summary>The friendly name shown in the provider selector.</summary>
    string DisplayName { get; }

    /// <summary>An example connection string shown as placeholder/guidance text.</summary>
    string ConnectionStringPlaceholder { get; }

    /// <summary>
    /// Creates a new, unopened connection for the given connection string.
    /// Call <see cref="IDatabaseConnection.OpenAsync"/> to establish it.
    /// </summary>
    IDatabaseConnection CreateConnection(string connectionString);

    /// <summary>
    /// Performs light, provider-specific validation of a connection string's shape
    /// before attempting to connect (e.g. required keywords). Returns an error message,
    /// or null if the string looks well-formed.
    /// </summary>
    string? ValidateConnectionString(string connectionString);
}
