namespace DatabaseExplorer.Core.Exceptions;

/// <summary>
/// Thrown when a metadata lookup or data query fails after a connection has already
/// been successfully established. The <see cref="Exception.Message"/> is always safe
/// to display directly to the user.
/// </summary>
public sealed class DatabaseQueryException : Exception
{
    public DatabaseQueryException(string userFriendlyMessage, Exception? inner = null)
        : base(userFriendlyMessage, inner)
    {
    }
}
