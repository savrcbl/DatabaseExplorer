using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Microsoft.Data.Sqlite;

namespace DatabaseExplorer.DataProviders.Sqlite;

/// <summary>
/// <see cref="IDatabaseProvider"/> implementation for SQLite, backed by
/// Microsoft.Data.Sqlite. Unlike SQL Server and PostgreSQL, SQLite is a serverless,
/// single-file database — the "connection string" is really just a file path (or
/// <c>:memory:</c>), and there is no authentication, server, or multi-schema concept.
/// </summary>
public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.Sqlite;

    public string DisplayName => "SQLite";

    public string ConnectionStringPlaceholder => @"Data Source=C:\path\to\database.db";

    public IDatabaseConnection CreateConnection(string connectionString) =>
        new SqliteDatabaseConnection(connectionString);

    public string? ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "Connection string cannot be empty.";
        }

        try
        {
            // Parsing via the builder both validates syntax and normalizes keywords.
            _ = new SqliteConnectionStringBuilder(connectionString);
            return null;
        }
        catch (Exception ex)
        {
            return $"The connection string is not valid: {ex.Message}";
        }
    }
}
