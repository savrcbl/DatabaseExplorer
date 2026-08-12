namespace DatabaseExplorer.Core.Models;

public sealed record ViewInfo(string Schema, string Name)
{
    public string QualifiedName => $"{Schema}.{Name}";
}
