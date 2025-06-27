namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Represents the state of a SignalR connection
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Failed
    }
}