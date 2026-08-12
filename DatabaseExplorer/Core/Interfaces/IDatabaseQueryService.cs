using System.Threading;
using System.Threading.Tasks;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

public interface IDatabaseQueryService
{
    Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TableInfo>> GetTablesAsync(string schema, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ViewInfo>> GetViewsAsync(string schema, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(string schema, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string schema, string objectName, CancellationToken cancellationToken = default);

    Task<QueryResult> GetObjectDataAsync(
        string schema,
        string objectName,
        int? rowLimit,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default);

    Task<long> GetRowCountAsync(string schema, string objectName, CancellationToken cancellationToken = default);

    Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default);
}
