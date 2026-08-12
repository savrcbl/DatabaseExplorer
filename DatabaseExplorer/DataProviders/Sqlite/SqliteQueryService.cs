using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Microsoft.Data.Sqlite;

namespace DatabaseExplorer.DataProviders.Sqlite;

public sealed class SqliteQueryService : IDatabaseQueryService
{
    private readonly SqliteConnection _connection;

    public SqliteQueryService(SqliteConnection connection)
    {
        _connection = connection;
    }

    public Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchemaInfo> schemas = [new SchemaInfo("main")];
        return Task.FromResult(schemas);
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite\_%' ESCAPE '\'
            ORDER BY name;
            """;

        return await ExecuteListAsync(sql, reader => new TableInfo("main", reader.GetString(0)), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ViewInfo>> GetViewsAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT name FROM sqlite_master
            WHERE type = 'view'
            ORDER BY name;
            """;

        return await ExecuteListAsync(sql, reader => new ViewInfo("main", reader.GetString(0)), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(string schema, CancellationToken cancellationToken = default)
    {
        // SQLite has no stored procedures. The nav tree already only shows a
        // "Procedures" folder for schemas that have at least one, so this simply
        // results in no such folder appearing for SQLite databases.
        IReadOnlyList<ProcedureInfo> none = [];
        return Task.FromResult(none);
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        // PRAGMA statements don't accept bound parameters for their own arguments in
        // SQLite, so the object name is quoted and inlined rather than parameterized.
        // objectName always comes from our own prior GetTablesAsync/GetViewsAsync
        // results, never directly from free-text user input.
        var sql = $"PRAGMA table_info({QuoteIdentifier(objectName)});";

        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            var results = new List<ColumnInfo>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // PRAGMA table_info columns, in order: cid, name, type, notnull, dflt_value, pk.
                var name = reader.GetString(1);
                var declaredType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var dataType = string.IsNullOrWhiteSpace(declaredType) ? "TEXT" : declaredType;
                var notNull = reader.GetInt32(3) == 1;
                var isPrimaryKey = reader.GetInt32(5) > 0;

                results.Add(new ColumnInfo(
                    Name: name,
                    DataType: dataType,
                    IsNullable: !notNull,
                    IsPrimaryKey: isPrimaryKey,
                    MaxLength: null, // SQLite uses type affinity, not a fixed max length
                    OrdinalPosition: results.Count + 1));
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException ex)
        {
            throw new DatabaseQueryException($"Metadata query failed: {ex.Message}", ex);
        }
    }

    public async Task<QueryResult> GetObjectDataAsync(
        string schema,
        string objectName,
        int? rowLimit,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            progress?.Report(rowLimit is int limit
                ? $"Loading first {limit:N0} row(s) of {objectName}..."
                : $"Loading {objectName}...");

            var limitClause = rowLimit is int ? " LIMIT @rowLimit" : string.Empty;
            var sql = $"SELECT * FROM {QuoteIdentifier(objectName)}{limitClause};";
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;

            if (rowLimit is int limitValue)
            {
                command.Parameters.AddWithValue("@rowLimit", limitValue);
            }

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            var table = new DataTable(objectName);
            table.Load(reader);

            stopwatch.Stop();
            progress?.Report($"Loaded {table.Rows.Count:N0} row(s) from {objectName}.");

            return new QueryResult
            {
                Data = table,
                SourceName = objectName, // no "schema." prefix — SQLite has no real schemas
                Elapsed = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException ex)
        {
            throw new DatabaseQueryException($"Could not load data from '{objectName}': {ex.Message}", ex);
        }
    }

    public async Task<long> GetRowCountAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM {QuoteIdentifier(objectName)};";

        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is long count ? count : Convert.ToInt64(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException ex)
        {
            throw new DatabaseQueryException($"Could not determine row count for '{objectName}': {ex.Message}", ex);
        }
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            progress?.Report("Running query...");

            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            var table = new DataTable("Result");

            if (reader.FieldCount > 0)
            {
                table.Load(reader);
            }
            else
            {
                // The statement didn't return a result set (INSERT/UPDATE/DELETE/DDL) —
                // surface the affected row count instead of an empty grid.
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    // Nothing to read for a non-query statement; drain defensively.
                }

                table.Columns.Add("RowsAffected", typeof(int));
                table.Rows.Add(reader.RecordsAffected < 0 ? 0 : reader.RecordsAffected);
            }

            stopwatch.Stop();
            progress?.Report($"Query completed — {table.Rows.Count:N0} row(s), {table.Columns.Count} column(s).");

            return new QueryResult
            {
                Data = table,
                SourceName = "(custom query)",
                Elapsed = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException ex)
        {
            throw new DatabaseQueryException($"Query failed: {ex.Message}", ex);
        }
    }

    /// <summary>Wraps an identifier in double quotes, escaping any embedded quotes.</summary>
    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private async Task<IReadOnlyList<T>> ExecuteListAsync<T>(
        string sql,
        Func<SqliteDataReader, T> map,
        CancellationToken cancellationToken)
    {
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            var results = new List<T>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(map(reader));
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException ex)
        {
            throw new DatabaseQueryException($"Metadata query failed: {ex.Message}", ex);
        }
    }
}
