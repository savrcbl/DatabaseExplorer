namespace DatabaseExplorer.Core.Models;

public sealed record ProcedureInfo(string Schema, string Name)
{
    public string QualifiedName => $"{Schema}.{Name}";
}
