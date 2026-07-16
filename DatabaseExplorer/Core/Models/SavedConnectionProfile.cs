namespace DatabaseExplorer.Core.Models;

/// <summary>
/// A user-named connection profile persisted locally so it can be re-selected without
/// retyping a connection string. The connection string itself is stored encrypted at
/// rest (see <see cref="Core.Interfaces.IConnectionProfileStore"/>) — this record only
/// ever holds the plaintext value in memory, after the store has decrypted it.
/// </summary>
/// <param name="Name">The user-chosen display name, e.g. "Local dev" or "Staging".</param>
/// <param name="ProviderType">Which engine this profile connects to.</param>
/// <param name="ConnectionString">The plaintext connection string.</param>
public sealed record SavedConnectionProfile(string Name, DatabaseProviderType ProviderType, string ConnectionString);
