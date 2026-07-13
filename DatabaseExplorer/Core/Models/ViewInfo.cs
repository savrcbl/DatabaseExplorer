namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Describes a database view within a schema.
/// </summary>
/// <param name="Schema">The owning schema name.</param>
/// <param name="Name">The view name.</param>
public sealed record ViewInfo(string Schema, string Name)
{
    /// <summary>
    /// The fully-qualified, human-readable name, e.g. "dbo.ActiveCustomers".
    /// </summary>
    public string QualifiedName => $"{Schema}.{Name}";
}
