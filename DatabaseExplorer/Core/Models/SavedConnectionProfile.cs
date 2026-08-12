namespace DatabaseExplorer.Core.Models;

public sealed record SavedConnectionProfile(string Name, DatabaseProviderType ProviderType, string ConnectionString);
