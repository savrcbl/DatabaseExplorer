using System.Data;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

public interface IDatabaseConnection : IAsyncDisposable
{
    DatabaseProviderType ProviderType { get; }

    ConnectionState State { get; }

    string? DatabaseName { get; }

    string? ServerName { get; }

    IDatabaseQueryService QueryService { get; }

    Task OpenAsync(CancellationToken cancellationToken = default);

    Task CloseAsync();

    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
