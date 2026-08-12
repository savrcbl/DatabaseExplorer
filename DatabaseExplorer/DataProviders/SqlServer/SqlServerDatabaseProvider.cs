using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;
using Microsoft.Data.SqlClient;

namespace DatabaseExplorer.DataProviders.SqlServer;

public sealed class SqlServerDatabaseProvider : IDatabaseProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.SqlServer;

    public string DisplayName => "Microsoft SQL Server";

    public string ConnectionStringPlaceholder =>
        "Server=localhost;Database=MyDatabase;User Id=sa;Password=your_password;TrustServerCertificate=True;";

    public IDatabaseConnection CreateConnection(string connectionString) =>
        new SqlServerDatabaseConnection(connectionString);

    public string? ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "Connection string cannot be empty.";
        }

        try
        {
            // Parsing via the builder both validates syntax and normalizes keywords.
            _ = new SqlConnectionStringBuilder(connectionString);
            return null;
        }
        catch (Exception ex)
        {
            return $"The connection string is not valid: {ex.Message}";
        }
    }
}
