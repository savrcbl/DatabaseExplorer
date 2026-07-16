using System.Threading;
using System.Threading.Tasks;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

/// <summary>
/// Provides metadata enumeration and data retrieval for a single open database connection.
/// Implementations are provider-specific (SQL Server, PostgreSQL, ...) but expose a
/// uniform, engine-agnostic surface to the rest of the application.
/// </summary>
public interface IDatabaseQueryService
{
    /// <summary>Enumerates the user-visible schemas in the current database.</summary>
    Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(CancellationToken cancellationToken = default);

    /// <summary>Enumerates the tables defined within <paramref name="schema"/>.</summary>
    Task<IReadOnlyList<TableInfo>> GetTablesAsync(string schema, CancellationToken cancellationToken = default);

    /// <summary>Enumerates the views defined within <paramref name="schema"/>.</summary>
    Task<IReadOnlyList<ViewInfo>> GetViewsAsync(string schema, CancellationToken cancellationToken = default);

    /// <summary>Enumerates the stored procedures defined within <paramref name="schema"/>.</summary>
    Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(string schema, CancellationToken cancellationToken = default);

    /// <summary>Enumerates the columns of a table or view, in ordinal order.</summary>
    Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string schema, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <c>SELECT * FROM [schema].[table]</c> (or the provider's equivalent syntax)
    /// and materializes the result set.
    /// </summary>
    /// <param name="schema">The owning schema.</param>
    /// <param name="objectName">The table or view name.</param>
    /// <param name="rowLimit">
    /// When set, caps the query to the first N rows (e.g. via <c>TOP (N)</c> or
    /// <c>LIMIT N</c>, whichever the provider uses) instead of loading the entire table.
    /// Pass null to load every row.
    /// </param>
    /// <param name="progress">Optional progress reporter for status-bar updates.</param>
    /// <param name="cancellationToken">Allows the caller to cancel a long-running load.</param>
    Task<QueryResult> GetObjectDataAsync(
        string schema,
        string objectName,
        int? rowLimit,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the total row count for a table or view without loading its data.</summary>
    Task<long> GetRowCountAsync(string schema, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an arbitrary, user-supplied SQL statement (the query editor's "Run"
    /// action). Works for statements that return a result set (SELECT and similar) as
    /// well as ones that don't (INSERT/UPDATE/DELETE/DDL) — the latter are reported back
    /// as a single-row "rows affected" result rather than an empty grid.
    /// </summary>
    /// <param name="sql">The raw SQL text to execute, exactly as the user wrote it.</param>
    /// <param name="progress">Optional progress reporter for status-bar updates.</param>
    /// <param name="cancellationToken">Allows the caller to cancel a long-running query.</param>
    Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default);
}
