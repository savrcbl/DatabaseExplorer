using System.Data;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Npgsql;

namespace DatabaseExplorer.DataProviders.PostgreSql;

/// <summary>
/// <see cref="IDatabaseConnection"/> implementation for PostgreSQL.
/// </summary>
public sealed class PostgreSqlDatabaseConnection : IDatabaseConnection
{
    private readonly NpgsqlConnection _connection;
    private bool _disposed;

    public PostgreSqlDatabaseConnection(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        QueryService = new PostgreSqlQueryService(_connection);
    }

    public DatabaseProviderType ProviderType => DatabaseProviderType.PostgreSql;

    public ConnectionState State => _connection.State;

    public string? DatabaseName => _connection.State == ConnectionState.Open ? _connection.Database : null;

    public string? ServerName => _connection.State == ConnectionState.Open ? _connection.Host : null;

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
        catch (PostgresException ex)
        {
            throw new DatabaseConnectionException(TranslatePostgresException(ex), ClassifyPostgresException(ex), ex);
        }
        catch (NpgsqlException ex)
        {
            throw new DatabaseConnectionException(TranslateNpgsqlException(ex), ClassifyNpgsqlException(ex), ex);
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
                $"Could not connect to PostgreSQL: {ex.Message}",
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

    private static string TranslatePostgresException(PostgresException ex) => ex.SqlState switch
    {
        "28P01" or "28000" => "Login failed. Please check the username and password.",
        "3D000" => "The specified database does not exist.",
        "42501" => "The role does not have permission to access this database.",
        _ => $"Could not connect to PostgreSQL: {ex.MessageText}"
    };

    private static DatabaseConnectionFailureReason ClassifyPostgresException(PostgresException ex) => ex.SqlState switch
    {
        "28P01" or "28000" => DatabaseConnectionFailureReason.AuthenticationFailed,
        "3D000" => DatabaseConnectionFailureReason.DatabaseNotFound,
        "42501" => DatabaseConnectionFailureReason.PermissionDenied,
        _ => DatabaseConnectionFailureReason.Unknown
    };

    private static string TranslateNpgsqlException(NpgsqlException ex)
    {
        if (ex.InnerException is System.Net.Sockets.SocketException)
        {
            return "The server could not be found or is not accepting connections. Please check the host and port.";
        }

        if (ex is NpgsqlException && ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "The connection attempt timed out. The server may be unreachable or overloaded.";
        }

        return $"Could not connect to PostgreSQL: {ex.Message}";
    }

    private static DatabaseConnectionFailureReason ClassifyNpgsqlException(NpgsqlException ex)
    {
        if (ex.InnerException is System.Net.Sockets.SocketException)
        {
            return DatabaseConnectionFailureReason.ServerNotFound;
        }

        if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseConnectionFailureReason.Timeout;
        }

        return DatabaseConnectionFailureReason.NetworkError;
    }

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
