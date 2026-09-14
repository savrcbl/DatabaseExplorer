using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DatabaseExplorer.Core.Exceptions;
using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.DataProviders.Supabase;

public sealed class SupabaseQueryService : IDatabaseQueryService
{
    private readonly SupabaseDatabaseConnection _connection;

    public SupabaseQueryService(SupabaseDatabaseConnection connection)
    {
        _connection = connection;
    }

    public Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SchemaInfo>>(new[] { new SchemaInfo("public") });

    public Task<IReadOnlyList<TableInfo>> GetTablesAsync(string schema, CancellationToken cancellationToken = default)
    {
        var definitions = _connection.OpenApiSpec?["definitions"] as JsonObject;
        var names = definitions?.Select(kvp => kvp.Key) ?? Enumerable.Empty<string>();

        var tables = names
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new TableInfo(schema, n))
            .ToList();

        return Task.FromResult<IReadOnlyList<TableInfo>>(tables);
    }

    public Task<IReadOnlyList<ViewInfo>> GetViewsAsync(string schema, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ViewInfo>>(Array.Empty<ViewInfo>());

    public Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(string schema, CancellationToken cancellationToken = default)
    {
        var paths = _connection.OpenApiSpec?["paths"] as JsonObject;

        var procedures = (paths?.Select(kvp => kvp.Key) ?? Enumerable.Empty<string>())
            .Where(p => p.StartsWith("/rpc/", StringComparison.OrdinalIgnoreCase))
            .Select(p => p["/rpc/".Length..])
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new ProcedureInfo(schema, n))
            .ToList();

        return Task.FromResult<IReadOnlyList<ProcedureInfo>>(procedures);
    }

    public Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        var definition = _connection.OpenApiSpec?["definitions"]?[objectName] as JsonObject;
        var properties = definition?["properties"] as JsonObject;
        var required = (definition?["required"] as JsonArray)?
            .Select(n => n?.GetValue<string>())
            .Where(n => n is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var columns = new List<ColumnInfo>();
        var position = 0;

        if (properties is not null)
        {
            foreach (var (name, node) in properties)
            {
                position++;
                var description = node?["description"]?.GetValue<string>() ?? string.Empty;
                var type = node?["format"]?.GetValue<string>() ?? node?["type"]?.GetValue<string>() ?? "unknown";
                var isPrimaryKey = description.Contains("Primary Key", StringComparison.OrdinalIgnoreCase);
                var isNullable = !required.Contains(name!);

                columns.Add(new ColumnInfo(name!, type, isNullable, isPrimaryKey, null, position));
            }
        }

        return Task.FromResult<IReadOnlyList<ColumnInfo>>(columns);
    }

    public async Task<QueryResult> GetObjectDataAsync(
        string schema,
        string objectName,
        int? rowLimit,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = rowLimit is int limit
            ? $"rest/v1/{objectName}?select=*&limit={limit}"
            : $"rest/v1/{objectName}?select=*";

        try
        {
            progress?.Report(rowLimit is int l
                ? $"Loading first {l:N0} row(s) of {objectName}..."
                : $"Loading {objectName}...");

            using var response = await _connection.HttpClient.GetAsync(query, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var table = BuildDataTable(objectName, body);

            stopwatch.Stop();
            progress?.Report($"Loaded {table.Rows.Count:N0} row(s) from {objectName}.");

            return new QueryResult { Data = table, SourceName = $"{schema}.{objectName}", Elapsed = stopwatch.Elapsed };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseQueryException($"Could not load data from '{objectName}': {ex.Message}", ex);
        }
    }

    public Task<QueryResult> ExecuteQueryAsync(string sql, IProgress<string>? progress, CancellationToken cancellationToken = default) =>
        throw new DatabaseQueryException(
            "Supabase REST connections don't support raw SQL. Expose a Postgres function (RPC) instead — it will show up under Procedures.");

    public async Task<long> GetRowCountAsync(string schema, string objectName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, $"rest/v1/{objectName}?select=*");
            request.Headers.Add("Prefer", "count=exact");

            using var response = await _connection.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string? contentRange = null;
            if (response.Content.Headers.TryGetValues("Content-Range", out var values))
            {
                contentRange = values.FirstOrDefault();
            }

            var slashIndex = contentRange?.IndexOf('/') ?? -1;
            if (slashIndex >= 0 && slashIndex < contentRange!.Length - 1)
            {
                var totalPart = contentRange[(slashIndex + 1)..];
                if (long.TryParse(totalPart, out var total))
                {
                    return total;
                }
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseQueryException($"Could not determine row count for '{schema}.{objectName}': {ex.Message}", ex);
        }
    }

    private static DataTable BuildDataTable(string tableName, string json)
    {
        var table = new DataTable(tableName);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return table;
        }

        foreach (var prop in doc.RootElement[0].EnumerateObject())
        {
            table.Columns.Add(prop.Name, typeof(string));
        }

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var values = new object?[table.Columns.Count];
            var i = 0;

            foreach (var prop in row.EnumerateObject())
            {
                values[i++] = prop.Value.ValueKind switch
                {
                    JsonValueKind.Null => DBNull.Value,
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.Value.GetRawText()
                };
            }

            table.Rows.Add(values);
        }

        return table;
    }
}
