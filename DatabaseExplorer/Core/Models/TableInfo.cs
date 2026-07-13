namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Describes a physical table within a schema.
/// </summary>
/// <param name="Schema">The owning schema name.</param>
/// <param name="Name">The table name.</param>
public sealed record TableInfo(string Schema, string Name)
{
    /// <summary>
    /// The fully-qualified, human-readable name, e.g. "dbo.Customers".
    /// </summary>
    public string QualifiedName => $"{Schema}.{Name}";
}
