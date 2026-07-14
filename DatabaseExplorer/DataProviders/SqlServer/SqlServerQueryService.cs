using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Microsoft.Data.SqlClient;

namespace DatabaseExplorer.DataProviders.SqlServer;

/// <summary>
/// <see cref="IDatabaseQueryService"/> implementation for Microsoft SQL Server. Uses
/// ANSI INFORMATION_SCHEMA views where possible for portability, falling back to
/// SQL Server's sys.* catalog views only where INFORMATION_SCHEMA has no equivalent.
/// </summary>
public sealed class SqlServerQueryService : IDatabaseQueryService
{
    private readonly SqlConnection _connection;

    public SqlServerQueryService(SqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.name AS SchemaName
            FROM sys.schemas s
            INNER JOIN sys.sysusers u ON u.uid = s.principal_id
            WHERE s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest')
              AND s.name NOT LIKE 'db[_]%'
              AND EXISTS (
                  SELECT 1 FROM sys.objects o
                  WHERE o.schema_id = s.schema_id AND o.type IN ('U', 'V', 'P')
              )
            ORDER BY s.name;
            """;

        return await ExecuteListAsync(sql, reader => new SchemaInfo(reader.GetString(0)), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME;
            """;

        return await ExecuteListAsync(
            sql,
            reader => new TableInfo(reader.GetString(0), reader.GetString(1)),
            cancellationToken,
            ("@schema", schema)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ViewInfo>> GetViewsAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.VIEWS
            WHERE TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME;
            """;

        return await ExecuteListAsync(
            sql,
            reader => new ViewInfo(reader.GetString(0), reader.GetString(1)),
            cancellationToken,
            ("@schema", schema)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(string schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ROUTINE_SCHEMA, ROUTINE_NAME
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_TYPE = 'PROCEDURE' AND ROUTINE_SCHEMA = @schema
            ORDER BY ROUTINE_NAME;
            """;

        return await ExecuteListAsync(
            sql,
            reader => new ProcedureInfo(reader.GetString(0), reader.GetString(1)),
            cancellationToken,
            ("@schema", schema)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IS_NULLABLE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON pk.TABLE_SCHEMA = c.TABLE_SCHEMA AND pk.TABLE_NAME = c.TABLE_NAME AND pk.COLUMN_NAME = c.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION;
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
            ("@schema", schema), ("@table", objectName)).ConfigureAwait(false);
    }

    public async Task<QueryResult> GetObjectDataAsync(
        string schema,
        string objectName,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        var qualifiedName = $"{schema}.{objectName}";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            progress?.Report($"Loading {qualifiedName}...");

            var sql = $"SELECT * FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(objectName)};";
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            var table = new DataTable(objectName);
            table.Load(reader);

            stopwatch.Stop();
            progress?.Report($"Loaded {table.Rows.Count:N0} rows from {qualifiedName}.");

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
        catch (SqlException ex)
        {
            throw new DatabaseQueryException($"Could not load data from '{qualifiedName}': {ex.Message}", ex);
        }
    }

    public async Task<long> GetRowCountAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT_BIG(*) FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(objectName)};";

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
        catch (SqlException ex)
        {
            throw new DatabaseQueryException($"Could not determine row count for '{schema}.{objectName}': {ex.Message}", ex);
        }
    }

    /// <summary>Wraps an identifier in square brackets, escaping any embedded brackets.</summary>
    private static string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]")}]";

    private async Task<IReadOnlyList<T>> ExecuteListAsync<T>(
        string sql,
        Func<SqlDataReader, T> map,
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
        catch (SqlException ex)
        {
            throw new DatabaseQueryException($"Metadata query failed: {ex.Message}", ex);
        }
    }
}
