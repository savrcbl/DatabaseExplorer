namespace DatabaseExplorer.Core.Models;

/// <summary>
/// A display-friendly wrapper around <see cref="DatabaseProviderType"/> used to populate
/// the provider selector in the navigation panel.
/// </summary>
/// <param name="Type">The underlying provider type.</param>
/// <param name="DisplayName">The friendly name shown to the user.</param>
/// <param name="ConnectionStringPlaceholder">
/// An example connection string shown as placeholder/guidance text.
/// </param>
public sealed record ProviderOption(
    DatabaseProviderType Type,
    string DisplayName,
    string ConnectionStringPlaceholder);
