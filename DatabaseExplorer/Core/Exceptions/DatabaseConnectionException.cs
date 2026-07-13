namespace DatabaseExplorer.Core.Exceptions;

/// <summary>
/// Thrown when a database connection cannot be opened or validated. The
/// <see cref="Exception.Message"/> is always safe to display directly to the user;
/// the original provider exception is preserved as <see cref="Exception.InnerException"/>
/// for diagnostics/logging.
/// </summary>
public sealed class DatabaseConnectionException : Exception
{
    /// <summary>
    /// A coarse classification of the failure, used to tailor guidance in the UI.
    /// </summary>
    public DatabaseConnectionFailureReason Reason { get; }

    public DatabaseConnectionException(string userFriendlyMessage, DatabaseConnectionFailureReason reason, Exception? inner = null)
        : base(userFriendlyMessage, inner)
    {
        Reason = reason;
    }
}

/// <summary>
/// Coarse classification of why a connection attempt failed.
/// </summary>
public enum DatabaseConnectionFailureReason
{
    Unknown,
    InvalidConnectionString,
    AuthenticationFailed,
    Timeout,
    ServerNotFound,
    DatabaseNotFound,
    PermissionDenied,
    NetworkError
}
