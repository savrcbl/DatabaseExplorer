namespace DatabaseExplorer.Core.Models;
record ProviderOption(
    DatabaseProviderType Type,
    string DisplayName,
    string ConnectionStringPlaceholder);
