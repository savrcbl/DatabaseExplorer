using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Npgsql;

namespace DatabaseExplorer.DataProviders.PostgreSql;

/// <summary>
/// <see cref="IDatabaseProvider"/> implementation for PostgreSQL, backed by Npgsql.
/// </summary>
public sealed class PostgreSqlDatabaseProvider : IDatabaseProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.PostgreSql;

    public string DisplayName => "PostgreSQL";

    public string ConnectionStringPlaceholder =>
        "Host=localhost;Port=5432;Database=my_database;Username=postgres;Password=your_password;";

    public IDatabaseConnection CreateConnection(string connectionString) =>
        new PostgreSqlDatabaseConnection(connectionString);

    public string? ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "Connection string cannot be empty.";
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
            return null;
        }
        catch (Exception ex)
        {
            return $"The connection string is not valid: {ex.Message}";
        }
    }
}
