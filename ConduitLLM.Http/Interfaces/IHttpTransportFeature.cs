namespace ConduitLLM.Http.Interfaces;

/// <summary>
/// Interface to get transport type from SignalR features
/// </summary>
public interface IHttpTransportFeature
{
    /// <summary>
    /// Gets the transport type being used for the connection
    /// </summary>
    HttpTransportType TransportType { get; }
}

/// <summary>
/// HTTP transport types for SignalR connections
/// </summary>
public enum HttpTransportType
{
    /// <summary>
    /// WebSockets transport
    /// </summary>
    WebSockets,
    
    /// <summary>
    /// Server-Sent Events transport
    /// </summary>
    ServerSentEvents,
    
    /// <summary>
    /// Long Polling transport
    /// </summary>
    LongPolling
}