using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

public interface IDatabaseProvider
{
    DatabaseProviderType ProviderType { get; }

    string DisplayName { get; }

    string ConnectionStringPlaceholder { get; }

    IDatabaseConnection CreateConnection(string connectionString);

    string? ValidateConnectionString(string connectionString);
}
