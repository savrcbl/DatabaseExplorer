namespace DatabaseExplorer.Core.Models;

public sealed record ColumnInfo(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    int? MaxLength,
    int OrdinalPosition)
{
    public string DisplayType => MaxLength is > 0 ? $"{DataType}({MaxLength})" : DataType;
}
