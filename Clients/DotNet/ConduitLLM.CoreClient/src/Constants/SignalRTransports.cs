using Microsoft.AspNetCore.Http.Connections;

namespace ConduitLLM.CoreClient.Constants;

/// <summary>
/// SignalR transport configuration constants for type-safe connection setup.
/// </summary>
public static class SignalRTransports
{
    /// <summary>
    /// Default transport types supporting WebSockets and Server-Sent Events.
    /// </summary>
    public static readonly HttpTransportType DefaultTransports = 
        HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents;

    /// <summary>
    /// All available transport types including long polling as fallback.
    /// </summary>
    public static readonly HttpTransportType AllTransports = 
        HttpTransportType.WebSockets | 
        HttpTransportType.ServerSentEvents | 
        HttpTransportType.LongPolling;

    /// <summary>
    /// WebSockets only transport (highest performance).
    /// </summary>
    public static readonly HttpTransportType WebSocketsOnly = HttpTransportType.WebSockets;

    /// <summary>
    /// Fallback transports for restricted environments.
    /// </summary>
    public static readonly HttpTransportType FallbackTransports = 
        HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
}