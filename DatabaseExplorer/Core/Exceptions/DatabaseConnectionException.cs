namespace DatabaseExplorer.Core.Exceptions;

public sealed class DatabaseConnectionException : Exception
{
    public DatabaseConnectionFailureReason Reason { get; }

    public DatabaseConnectionException(string userFriendlyMessage, DatabaseConnectionFailureReason reason, Exception? inner = null)
        : base(userFriendlyMessage, inner)
    {
        Reason = reason;
    }
}

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
