namespace ConduitLLM.Gateway.Interfaces
{
    /// <summary>
    /// Interface for distributed monitoring services that store metrics centrally
    /// </summary>
    public interface IDistributedMonitoringService
    {
        /// <summary>
        /// Gets the unique instance identifier for this service instance
        /// </summary>
        string InstanceId { get; }

        /// <summary>
        /// Registers this service instance for distributed monitoring
        /// </summary>
        Task RegisterInstanceAsync();

        /// <summary>
        /// Unregisters this service instance from distributed monitoring
        /// </summary>
        Task UnregisterInstanceAsync();

        /// <summary>
        /// Updates the heartbeat for this service instance
        /// </summary>
        Task UpdateHeartbeatAsync();
    }

    /// <summary>
    /// Interface for distributed SignalR metrics with centralized connection tracking
    /// </summary>
    public interface IDistributedSignalRMetricsService : IDistributedMonitoringService
    {
        Task OnConnectedAsync(string connectionId, string hubName, string virtualKeyId);
        Task OnDisconnectedAsync(string connectionId, string? exception = null);
        Task OnReconnectedAsync(string connectionId, string hubName);
        Task OnMessageSentAsync(string hubName, string method, double processingTimeMs = 0);
        Task OnMessageReceivedAsync(string hubName, string method);
        Task OnTaskSubscribedAsync(string hubName, string taskType);
        Task OnTaskUnsubscribedAsync(string hubName, string taskType);
        Task<int> GetGlobalConnectionCountAsync();
        Task<int> GetConnectionCountForVirtualKeyAsync(string virtualKeyId);
        Task<bool> IsConnectionLimitReachedAsync(string virtualKeyId);
        Task<bool> IsGlobalConnectionLimitReachedAsync();
        Task<Dictionary<string, object>> GetAggregatedMetricsAsync();
        Task<List<string>> GetActiveInstancesAsync();
    }
}