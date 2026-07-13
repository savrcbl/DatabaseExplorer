namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Represents the application's high-level connection lifecycle, as distinct from the
/// low-level ADO.NET <see cref="System.Data.ConnectionState"/> of a single connection object.
/// </summary>
public enum AppConnectionState
{
    /// <summary>No active connection.</summary>
    Disconnected,

    /// <summary>A connection attempt is in progress.</summary>
    Connecting,

    /// <summary>Connected, but no metadata scan has completed yet.</summary>
    Connected,

    /// <summary>Connected and actively enumerating schemas/tables/views/procedures.</summary>
    Scanning,

    /// <summary>Connected and metadata is ready for browsing.</summary>
    Ready,

    /// <summary>The last operation failed; the connection may or may not still be usable.</summary>
    Error
}
