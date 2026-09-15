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

    public async Task<QueryResult> ExecuteQueryAsync(string queryText, IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var (functionName, paramsJson) = ParseRpcInput(queryText);

        try
        {
            progress?.Report($"Calling {functionName}...");

            using var content = new StringContent(paramsJson, System.Text.Encoding.UTF8, "application/json");
            using var response = await _connection.HttpClient.PostAsync($"rest/v1/rpc/{functionName}", content, cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new DatabaseQueryException(ExtractPostgrestErrorMessage(body, response.StatusCode, functionName));
            }

            var table = string.IsNullOrWhiteSpace(body)
                ? new DataTable(functionName)
                : BuildDataTableFromJson(functionName, body);

            stopwatch.Stop();
            progress?.Report($"{functionName} returned {table.Rows.Count:N0} row(s).");

            return new QueryResult { Data = table, SourceName = $"rpc/{functionName}", Elapsed = stopwatch.Elapsed };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DatabaseQueryException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseQueryException($"Could not call '{functionName}': {ex.Message}", ex);
        }
    }

    private static (string FunctionName, string ParamsJson) ParseRpcInput(string input)
    {
        var trimmed = input.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DatabaseQueryException(
                "Supabase connections call Postgres functions here, not raw SQL. Enter a function name, e.g. my_function or my_function {\"arg\": 1} — see it listed under Procedures.");
        }

        var spaceIndex = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        var functionName = spaceIndex < 0 ? trimmed : trimmed[..spaceIndex];
        var rest = spaceIndex < 0 ? string.Empty : trimmed[(spaceIndex + 1)..].Trim();
        var paramsJson = string.IsNullOrEmpty(rest) ? "{}" : rest;

        try
        {
            using var _ = JsonDocument.Parse(paramsJson);
        }
        catch (JsonException)
        {
            throw new DatabaseQueryException(
                $"The parameters after '{functionName}' must be valid JSON, e.g. {{\"arg\": 1}}.");
        }

        return (functionName, paramsJson);
    }

    private static string ExtractPostgrestErrorMessage(string body, System.Net.HttpStatusCode statusCode, string functionName)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var messageNode) && messageNode.ValueKind == JsonValueKind.String)
            {
                return $"'{functionName}' failed: {messageNode.GetString()}";
            }
        }
        catch (JsonException)
        {
        }

        return $"'{functionName}' failed with HTTP {(int)statusCode} {statusCode}.";
    }

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

    private static DataTable BuildDataTable(string tableName, string json) =>
        BuildDataTableFromJson(tableName, json);

    private static DataTable BuildDataTableFromJson(string sourceName, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return BuildDataTableFromArray(sourceName, root);
        }

        var table = new DataTable(sourceName);

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                table.Columns.Add(prop.Name, typeof(string));
            }

            var values = new object?[table.Columns.Count];
            var i = 0;
            foreach (var prop in root.EnumerateObject())
            {
                values[i++] = ValueToCell(prop.Value);
            }

            table.Rows.Add(values);
            return table;
        }

        table.Columns.Add("result", typeof(string));
        table.Rows.Add(root.ValueKind == JsonValueKind.Null ? DBNull.Value : (object)root.GetRawText());
        return table;
    }

    private static DataTable BuildDataTableFromArray(string tableName, JsonElement arrayElement)
    {
        var table = new DataTable(tableName);

        if (arrayElement.GetArrayLength() == 0)
        {
            return table;
        }

        foreach (var prop in arrayElement[0].EnumerateObject())
        {
            table.Columns.Add(prop.Name, typeof(string));
        }

        foreach (var row in arrayElement.EnumerateArray())
        {
            var values = new object?[table.Columns.Count];
            var i = 0;

            foreach (var prop in row.EnumerateObject())
            {
                values[i++] = ValueToCell(prop.Value);
            }

            table.Rows.Add(values);
        }

        return table;
    }

    private static object ValueToCell(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => DBNull.Value,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText()
    };
}
