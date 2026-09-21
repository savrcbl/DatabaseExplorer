using DatabaseExplorer.Core.Interfaces;
using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.DataProviders.Supabase;

public sealed class SupabaseDatabaseProvider : IDatabaseProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.Supabase;

    public string DisplayName => "Supabase";

    public string ConnectionStringPlaceholder =>
        "Url=https://YOUR_PROJECT.supabase.co;ApiKey=YOUR_SECRET_KEY;";

    public IDatabaseConnection CreateConnection(string connectionString) =>
        new SupabaseDatabaseConnection(connectionString);

    public string? ValidateConnectionString(string connectionString) =>
        SupabaseConnectionInfo.TryParse(connectionString, out _, out var error) ? null : error;
}
