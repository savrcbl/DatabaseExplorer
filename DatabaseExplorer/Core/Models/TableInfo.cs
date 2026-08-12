namespace DatabaseExplorer.Core.Models;

public sealed record TableInfo(string Schema, string Name)
{
    public string QualifiedName => $"{Schema}.{Name}";
}
