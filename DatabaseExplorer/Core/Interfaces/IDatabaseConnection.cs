using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

/// <summary>
/// Represents a single logical connection to a database instance, opened from a
/// connection string via an <see cref="IDatabaseProvider"/>.
/// </summary>
public interface IDatabaseConnection : IAsyncDisposable
{
    /// <summary>The engine this connection targets.</summary>
    DatabaseProviderType ProviderType { get; }

    /// <summary>The current ADO.NET connection state.</summary>
    ConnectionState State { get; }

    /// <summary>The resolved database name, available once the connection is open.</summary>
    string? DatabaseName { get; }

    /// <summary>The resolved server/host name, available once the connection is open.</summary>
    string? ServerName { get; }

    /// <summary>The metadata and data-access surface for this connection.</summary>
    IDatabaseQueryService QueryService { get; }

    /// <summary>
    /// Opens the underlying connection, translating provider-specific failures into
    /// <see cref="Core.Exceptions.DatabaseConnectionException"/> with a user-friendly message.
    /// </summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the underlying connection if open. Safe to call multiple times.</summary>
    Task CloseAsync();

    /// <summary>
    /// Verifies the connection is genuinely usable (round-trips a lightweight command).
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
