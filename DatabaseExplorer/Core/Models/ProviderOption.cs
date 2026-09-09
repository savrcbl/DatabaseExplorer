namespace DatabaseExplorer.Core.Models;
public record ProviderOption(
    DatabaseProviderType Type,
    string DisplayName,
    string ConnectionStringPlaceholder);
