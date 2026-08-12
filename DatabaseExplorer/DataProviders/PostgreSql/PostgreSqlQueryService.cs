using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Npgsql;

namespace DatabaseExplorer.DataProviders.PostgreSql;

public sealed class PostgreSqlQueryService : IDatabaseQueryService
{
    private readonly NpgsqlConnection _connection;

    public PostgreSqlQueryService(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT schema_name
            FROM information_schema.schemata
            WHERE schema_name NOT IN ('pg_catalog', 'information_schema')
              AND schema_name NOT LIKE 'pg_toast%'
              AND schema_name NOT LIKE 'pg_temp%'
            ORDER BY schema_name;
            """;

        return await ExecuteListAsync(sql, reader => new SchemaInfo(reader.GetString(0)), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE' AND table_schema = @schema
            ORDER BY table_name;
            """;

        return await ExecuteListAsync(
            sql,
            reader => new TableInfo(reader.GetString(0), reader.GetString(1)),
            cancellationToken,
            ("schema", schema)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ViewInfo>> GetViewsAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT table_schema, table_name
            FROM information_schema.views
            WHERE table_schema = @schema
            ORDER BY table_name;
            """;

        return await ExecuteListAsync(
            sql,
            reader => new ViewInfo(reader.GetString(0), reader.GetString(1)),
            cancellationToken,
            ("schema", schema)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT routine_schema, routine_name
            FROM information_schema.routines
            WHERE routine_type IN ('PROCEDURE', 'FUNCTION') AND routine_schema = @schema
            ORDER BY routine_name;
            """;

        return await ExecuteListAsync(
            sql,
            reader => new ProcedureInfo(reader.GetString(0), reader.GetString(1)),
            cancellationToken,
            ("schema", schema)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.column_name,
                c.data_type,
                CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END AS is_nullable,
                CASE WHEN pk.column_name IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key,
                c.character_maximum_length,
                c.ordinal_position
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.table_schema, ku.table_name, ku.column_name
                FROM information_schema.table_constraints tc
                INNER JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                    AND tc.table_schema = ku.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
            ) pk ON pk.table_schema = c.table_schema AND pk.table_name = c.table_name AND pk.column_name = c.column_name
            WHERE c.table_schema = @schema AND c.table_name = @table
            ORDER BY c.ordinal_position;
            """;

        return await ExecuteListAsync(
            sql,
            reader => new ColumnInfo(
                Name: reader.GetString(0),
                DataType: reader.GetString(1),
                IsNullable: reader.GetInt32(2) == 1,
                IsPrimaryKey: reader.GetInt32(3) == 1,
                MaxLength: reader.IsDBNull(4) ? null : reader.GetInt32(4),
                OrdinalPosition: reader.GetInt32(5)),
            cancellationToken,
            ("schema", schema), ("table", objectName)).ConfigureAwait(false);
    }

    public async Task<QueryResult> GetObjectDataAsync(
        string schema,
        string objectName,
        int? rowLimit,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        var qualifiedName = $"{schema}.{objectName}";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            progress?.Report(rowLimit is int limit
                ? $"Loading first {limit:N0} row(s) of {qualifiedName}..."
                : $"Loading {qualifiedName}...");

            var limitClause = rowLimit is int ? " LIMIT @rowLimit" : string.Empty;
            var sql = $"SELECT * FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(objectName)}{limitClause};";
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;

            if (rowLimit is int limitValue)
            {
                command.Parameters.AddWithValue("rowLimit", limitValue);
            }

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            var table = new DataTable(objectName);
            table.Load(reader);

            stopwatch.Stop();
            progress?.Report($"Loaded {table.Rows.Count:N0} row(s) from {qualifiedName}.");

            return new QueryResult
            {
                Data = table,
                SourceName = qualifiedName,
                Elapsed = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (NpgsqlException ex)
        {
            throw new DatabaseQueryException($"Could not load data from '{qualifiedName}': {ex.Message}", ex);
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
        catch (NpgsqlException ex)
        {
            throw new DatabaseQueryException($"Query failed: {ex.Message}", ex);
        }
    }

    public async Task<long> GetRowCountAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(objectName)};";

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
        catch (NpgsqlException ex)
        {
            throw new DatabaseQueryException($"Could not determine row count for '{schema}.{objectName}': {ex.Message}", ex);
        }
    }

    /// <summary>Wraps an identifier in double quotes, escaping any embedded quotes.</summary>
    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private async Task<IReadOnlyList<T>> ExecuteListAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] parameters)
    {
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

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
        catch (NpgsqlException ex)
        {
            throw new DatabaseQueryException($"Metadata query failed: {ex.Message}", ex);
        }
    }
}
