namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Describes a schema (namespace) within a database.
/// </summary>
/// <param name="Name">The schema name, e.g. "dbo" or "public".</param>
public sealed record SchemaInfo(string Name);
