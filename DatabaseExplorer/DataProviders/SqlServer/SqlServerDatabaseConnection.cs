using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Microsoft.Data.SqlClient;

namespace DatabaseExplorer.DataProviders.SqlServer;

/// <summary>
/// <see cref="IDatabaseConnection"/> implementation for Microsoft SQL Server.
/// </summary>
public sealed class SqlServerDatabaseConnection : IDatabaseConnection
{
    private readonly SqlConnection _connection;
    private bool _disposed;

    public SqlServerDatabaseConnection(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
        QueryService = new SqlServerQueryService(_connection);
    }

    public DatabaseProviderType ProviderType => DatabaseProviderType.SqlServer;

    public ConnectionState State => _connection.State;

    public string? DatabaseName => _connection.State == ConnectionState.Open ? _connection.Database : null;

    public string? ServerName => _connection.State == ConnectionState.Open ? _connection.DataSource : null;

    public IDatabaseQueryService QueryService { get; }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException ex)
        {
            throw new DatabaseConnectionException(TranslateSqlException(ex), ClassifySqlException(ex), ex);
        }
        catch (ArgumentException ex)
        {
            throw new DatabaseConnectionException(
                $"The connection string is invalid: {ex.Message}",
                DatabaseConnectionFailureReason.InvalidConnectionString,
                ex);
        }
        catch (Exception ex)
        {
            throw new DatabaseConnectionException(
                $"Could not connect to SQL Server: {ex.Message}",
                DatabaseConnectionFailureReason.Unknown,
                ex);
        }
    }

    public async Task CloseAsync()
    {
        if (_connection.State != ConnectionState.Closed)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 10;
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Maps a <see cref="SqlException"/> to a short, user-friendly message. SQL Server
    /// signals most connectivity failure classes via well-known error numbers.
    /// </summary>
    private static string TranslateSqlException(SqlException ex) => ClassifySqlException(ex) switch
    {
        DatabaseConnectionFailureReason.AuthenticationFailed =>
            "Login failed. Please check the username and password.",
        DatabaseConnectionFailureReason.ServerNotFound =>
            "The server could not be found or is not accepting connections. Please check the server name and that SQL Server is running.",
        DatabaseConnectionFailureReason.DatabaseNotFound =>
            "The specified database does not exist or is not accessible.",
        DatabaseConnectionFailureReason.Timeout =>
            "The connection attempt timed out. The server may be unreachable or overloaded.",
        DatabaseConnectionFailureReason.PermissionDenied =>
            "The login does not have permission to access this database.",
        DatabaseConnectionFailureReason.NetworkError =>
            "A network-related error occurred while establishing the connection.",
        _ => $"Could not connect to SQL Server: {ex.Message}"
    };

    private static DatabaseConnectionFailureReason ClassifySqlException(SqlException ex) => ex.Number switch
    {
        18456 or 18470 => DatabaseConnectionFailureReason.AuthenticationFailed,
        4060 => DatabaseConnectionFailureReason.DatabaseNotFound,
        229 or 230 or 262 or 297 => DatabaseConnectionFailureReason.PermissionDenied,
        -2 => DatabaseConnectionFailureReason.Timeout,
        53 or 40 or 11001 or 10061 => DatabaseConnectionFailureReason.ServerNotFound,
        _ when ex.Class >= 20 => DatabaseConnectionFailureReason.NetworkError,
        _ => DatabaseConnectionFailureReason.Unknown
    };

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
