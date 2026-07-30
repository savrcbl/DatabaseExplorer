using System.Data;

namespace DatabaseExplorer.Core.Models;

public sealed class QueryResult
{
    /// <summary>The retrieved data. Never null; may have zero rows.</summary>
    public required DataTable Data { get; init; }

    /// <summary>The fully-qualified name of the table or view that was queried.</summary>
    public required string SourceName { get; init; }

    /// <summary>The number of rows returned in <see cref="Data"/>.</summary>
    public int RowCount => Data.Rows.Count;

    /// <summary>The number of columns returned in <see cref="Data"/>.</summary>
    public int ColumnCount => Data.Columns.Count;

    /// <summary>How long the query took to execute and materialize.</summary>
    public TimeSpan Elapsed { get; init; }
}
