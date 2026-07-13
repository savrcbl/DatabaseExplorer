namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Describes a single column belonging to a table or view.
/// </summary>
/// <param name="Name">The column name.</param>
/// <param name="DataType">The provider-reported data type, e.g. "nvarchar" or "integer".</param>
/// <param name="IsNullable">Whether the column allows NULL values.</param>
/// <param name="IsPrimaryKey">Whether the column participates in the primary key.</param>
/// <param name="MaxLength">The maximum character length, if applicable to the type.</param>
/// <param name="OrdinalPosition">The 1-based column position within the table.</param>
public sealed record ColumnInfo(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    int? MaxLength,
    int OrdinalPosition)
{
    /// <summary>
    /// A short, human-friendly type summary, e.g. "nvarchar(50)" or "integer".
    /// </summary>
    public string DisplayType => MaxLength is > 0 ? $"{DataType}({MaxLength})" : DataType;
}
