using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Microsoft.Data.Sqlite;

namespace DatabaseExplorer.DataProviders.Sqlite;

public sealed class SqliteDatabaseConnection : IDatabaseConnection
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public SqliteDatabaseConnection(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        QueryService = new SqliteQueryService(_connection);
    }

    public DatabaseProviderType ProviderType => DatabaseProviderType.Sqlite;

    public ConnectionState State => _connection.State;

    public string? DatabaseName
    {
        get
        {
            if (_connection.State != ConnectionState.Open)
            {
                return null;
            }

            var dataSource = _connection.DataSource;
            return string.IsNullOrEmpty(dataSource) ? "main" : Path.GetFileName(dataSource);
        }
    }

    public string? ServerName
    {
        get
        {
            if (_connection.State != ConnectionState.Open)
            {
                return null;
            }

            var dataSource = _connection.DataSource;
            if (string.IsNullOrEmpty(dataSource))
            {
                return "(in-memory)";
            }

            var directory = Path.GetDirectoryName(dataSource);
            return string.IsNullOrEmpty(directory) ? "(local file)" : directory;
        }
    }

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
        catch (SqliteException ex)
        {
            throw new DatabaseConnectionException(TranslateSqliteException(ex), ClassifySqliteException(ex), ex);
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
                $"Could not open the SQLite database: {ex.Message}",
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

    private static string TranslateSqliteException(SqliteException ex) => ClassifySqliteException(ex) switch
    {
        DatabaseConnectionFailureReason.DatabaseNotFound =>
            "The database file could not be found or opened. Please check the file path in the connection string.",
        DatabaseConnectionFailureReason.PermissionDenied =>
            "The database file is read-only, locked by another process, or access was denied.",
        DatabaseConnectionFailureReason.InvalidConnectionString =>
            "The file exists but does not appear to be a valid SQLite database.",
        _ => $"Could not open the SQLite database: {ex.Message}"
    };

    private static DatabaseConnectionFailureReason ClassifySqliteException(SqliteException ex) => ex.SqliteErrorCode switch
    {
        14 => DatabaseConnectionFailureReason.DatabaseNotFound, // SQLITE_CANTOPEN
        26 => DatabaseConnectionFailureReason.InvalidConnectionString, // SQLITE_NOTADB
        8 => DatabaseConnectionFailureReason.PermissionDenied, // SQLITE_READONLY
        5 => DatabaseConnectionFailureReason.PermissionDenied, // SQLITE_BUSY (locked by another process)
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
