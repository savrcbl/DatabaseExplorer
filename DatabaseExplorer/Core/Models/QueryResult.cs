using System.Data;

namespace DatabaseExplorer.Core.Models;

public sealed class QueryResult
{
    public required DataTable Data { get; init; }
    public required string SourceName { get; init; }
    public int RowCount => Data.Rows.Count;
    public int ColumnCount => Data.Columns.Count;
    public TimeSpan Elapsed { get; init; }
}
