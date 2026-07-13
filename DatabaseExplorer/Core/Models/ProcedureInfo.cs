namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Describes a stored procedure within a schema.
/// </summary>
/// <param name="Schema">The owning schema name.</param>
/// <param name="Name">The procedure name.</param>
public sealed record ProcedureInfo(string Schema, string Name)
{
    /// <summary>
    /// The fully-qualified, human-readable name, e.g. "dbo.usp_GetCustomers".
    /// </summary>
    public string QualifiedName => $"{Schema}.{Name}";
}
