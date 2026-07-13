namespace DatabaseExplorer.Core.Exceptions;

public sealed class DatabaseQueryException : Exception
{
    public DatabaseQueryException(string userFriendlyMessage, Exception? inner = null)
        : base(userFriendlyMessage, inner)
    {
    }
}
