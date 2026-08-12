namespace DatabaseExplorer.Core.Models;

public enum AppConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Scanning,
    Ready,
    Error
}
