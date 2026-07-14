namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Enumerates the database engines supported by the application.
/// Adding a new engine starts with adding a new member here.
/// </summary>
public enum DatabaseProviderType
{
    /// <summary>Microsoft SQL Server (including Azure SQL).</summary>
    SqlServer,

    /// <summary>PostgreSQL.</summary>
    PostgreSql
}
