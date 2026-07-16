using DatabaseExplorer.Core.Models;

namespace DatabaseExplorer.Core.Interfaces;

/// <summary>
/// Persists named connection profiles ("Local dev", "Staging", ...) to local disk so
/// they can be reused across sessions without retyping a connection string. Connection
/// strings are encrypted at rest — see the concrete implementation for details — but
/// this is local-machine, current-Windows-user protection, not a secrets vault; treat it
/// as convenience rather than enterprise-grade credential storage.
/// </summary>
public interface IConnectionProfileStore
{
    /// <summary>Loads every saved profile, decrypted and ready to use.</summary>
    Task<IReadOnlyList<SavedConnectionProfile>> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves <paramref name="profile"/>, replacing any existing profile with the same
    /// <see cref="SavedConnectionProfile.Name"/> (case-insensitive).
    /// </summary>
    Task SaveAsync(SavedConnectionProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Removes the profile with the given name, if one exists. No-op otherwise.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
