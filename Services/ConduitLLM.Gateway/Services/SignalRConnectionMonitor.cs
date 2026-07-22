using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Interfaces;

using StackExchange.Redis;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR;

using SignalRConnectionInfo = ConduitLLM.Gateway.Models.ConnectionInfo;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service that monitors SignalR connections
    /// </summary>
    public interface ISignalRConnectionMonitor
    {
        /// <summary>
        /// Records a new connection
        /// </summary>
        Task OnConnectionAsync(string connectionId, string hubName, HubCallerContext context);

        /// <summary>
        /// Records a disconnection
        /// </summary>
        Task OnDisconnectionAsync(string connectionId);

        /// <summary>
        /// Records activity on a connection
        /// </summary>
        Task RecordActivityAsync(string connectionId);

        /// <summary>
        /// Adds a connection to a group
        /// </summary>
        Task AddToGroupAsync(string connectionId, string groupName);

        /// <summary>
        /// Removes a connection from a group
        /// </summary>
        Task RemoveFromGroupAsync(string connectionId, string groupName);

        /// <summary>
        /// Gets information about a specific connection
        /// </summary>
        Task<ConduitLLM.Gateway.Models.ConnectionInfo?> GetConnectionAsync(string connectionId);

        /// <summary>
        /// Gets all active connections
        /// </summary>
        Task<IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo>> GetActiveConnectionsAsync();

        /// <summary>
        /// Gets connections for a specific hub
        /// </summary>
        Task<IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo>> GetHubConnectionsAsync(string hubName);

        /// <summary>
        /// Gets connections for a specific virtual key
        /// </summary>
        Task<IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo>> GetVirtualKeyConnectionsAsync(int virtualKeyId);

        /// <summary>
        /// Gets connections in a specific group
        /// </summary>
        Task<IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo>> GetGroupConnectionsAsync(string groupName);

        /// <summary>
        /// Gets monitoring statistics
        /// </summary>
        Task<ConnectionStatistics> GetStatisticsAsync();

        /// <summary>
        /// Records a message sent to a connection
        /// </summary>
        Task RecordMessageSentAsync(string connectionId);

        /// <summary>
        /// Records a message acknowledged by a connection
        /// </summary>
        Task RecordMessageAcknowledgedAsync(string connectionId);
    }

    /// <summary>
    /// Statistics about SignalR connections
    /// </summary>
    public class ConnectionStatistics
    {
        public int TotalActiveConnections { get; set; }
        public Dictionary<string, int> ConnectionsByHub { get; set; } = new();
        public Dictionary<string, int> ConnectionsByTransport { get; set; } = new();
        public int TotalGroups { get; set; }
        public int StaleConnections { get; set; }
        public double AverageConnectionDurationMinutes { get; set; }
        public double AverageIdleTimeMinutes { get; set; }
        public DateTime OldestConnectionTime { get; set; }
        public DateTime NewestConnectionTime { get; set; }
        public long TotalMessagesSent { get; set; }
        public long TotalMessagesAcknowledged { get; set; }
        public double AcknowledgmentRate { get; set; }
    }

    /// <summary>
    /// Implementation of SignalR connection monitor using Redis
    /// </summary>
    public partial class SignalRConnectionMonitor : ISignalRConnectionMonitor, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRConnectionMonitor> _logger;
        private readonly IConfiguration _configuration;
        private readonly RedisConnectionFactory _redisConnectionFactory;
        
        private Timer? _cleanupTimer;
        private IDatabase? _redis;
        private IServer? _server;

        // Redis keys
        private readonly string _connectionsKey;
        private readonly string _groupConnectionsKeyPrefix;
        
        private readonly TimeSpan _staleConnectionThreshold;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _connectionFieldTtl;
        private readonly bool _enableHashFieldExpiration;
        private bool _hashFieldExpirationSupported;

        private static readonly Version RedisHashFieldExpirationMinimumVersion = new(7, 4);

        public SignalRConnectionMonitor(
            ILogger<SignalRConnectionMonitor> logger,
            IConfiguration configuration,
            RedisConnectionFactory redisConnectionFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _redisConnectionFactory = redisConnectionFactory;

            // Redis keys
            _connectionsKey = RedisKeys.SignalR.ActiveConnections;
            _groupConnectionsKeyPrefix = RedisKeys.SignalR.GroupConnectionsPrefix;

            _staleConnectionThreshold = TimeSpan.FromMinutes(
                configuration.GetValue<int>("SignalR:ConnectionMonitor:StaleThresholdMinutes", 60));
            _cleanupInterval = TimeSpan.FromMinutes(
                configuration.GetValue<int>("SignalR:ConnectionMonitor:CleanupIntervalMinutes", 5));
            _connectionFieldTtl = _staleConnectionThreshold + (_cleanupInterval * 2);
            _enableHashFieldExpiration = configuration.GetValue(
                "SignalR:ConnectionMonitor:EnableHashFieldExpiration", true);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Connection Monitor starting");

            try
            {
                var connection = await _redisConnectionFactory.GetConnectionAsync();
                _redis = connection.GetDatabase();
                _server = connection.GetPrimaryServer();
                _hashFieldExpirationSupported = SupportsHashFieldExpiration(
                    _server.Version,
                    _enableHashFieldExpiration);

                _cleanupTimer = new Timer(
                    CleanupStaleConnections,
                    null,
                    _cleanupInterval,
                    _cleanupInterval);

                _logger.LogInformation(
                    "SignalR Connection Monitor started with Redis backend; hash field expiration {HashFieldExpirationStatus}",
                    _hashFieldExpirationSupported ? "enabled" : "unavailable (periodic cleanup fallback active)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SignalR Connection Monitor");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Connection Monitor stopping");

            _cleanupTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public async Task OnConnectionAsync(string connectionId, string hubName, HubCallerContext context)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot track connection {ConnectionId}", connectionId);
                return;
            }

            var connectionInfo = new SignalRConnectionInfo
            {
                ConnectionId = connectionId,
                HubName = hubName,
                ConnectedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                UserAgent = context.GetHttpContext()?.Request.Headers["User-Agent"].ToString(),
                IpAddress = context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
                TransportType = context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString()
            };

            // Extract virtual key ID from context
            if (context.Items.TryGetValue("VirtualKeyId", out var virtualKeyIdObj) && 
                virtualKeyIdObj is int virtualKeyId)
            {
                connectionInfo.VirtualKeyId = virtualKeyId;
            }

            try
            {
                var connectionData = JsonSerializer.Serialize(connectionInfo);
                await _redis.HashSetAsync(_connectionsKey, connectionId, connectionData);
                await RefreshConnectionFieldExpirationAsync(connectionId);

                _logger.LogDebug(
                    "Connection {ConnectionId} established on {HubName} from {IpAddress} using {Transport}",
                    connectionId, hubName, connectionInfo.IpAddress, connectionInfo.TransportType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track connection {ConnectionId} on hub {HubName}", connectionId, hubName);
                // Don't throw - connection tracking failure shouldn't break the connection
            }
        }

        public async Task OnDisconnectionAsync(string connectionId)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot remove connection {ConnectionId}", connectionId);
                return;
            }

            try
            {
                // Get connection info before removing it
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                SignalRConnectionInfo? connectionInfo = null;

                if (connectionData.HasValue)
                {
                    try
                    {
                        connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize connection info for {ConnectionId}", connectionId);
                    }
                }

                // Remove from connections hash
                await _redis.HashDeleteAsync(_connectionsKey, connectionId);

                // Remove from all groups - scan group keys for this connection
                if (_server != null)
                {
                    var tasks = new List<Task>();
                    foreach (var groupKey in _server.Keys(pattern: $"{_groupConnectionsKeyPrefix}:*"))
                    {
                        tasks.Add(_redis.SetRemoveAsync(groupKey, connectionId));
                    }
                    await Task.WhenAll(tasks);
                }

                if (connectionInfo != null)
                {
                    _logger.LogDebug(
                        "Connection {ConnectionId} disconnected after {Duration}min with {MessagesSent} messages sent, {MessagesAcked} acknowledged",
                        connectionId, 
                        connectionInfo.ConnectionDuration.TotalMinutes,
                        connectionInfo.MessagesSent,
                        connectionInfo.MessagesAcknowledged);
                }
                else
                {
                    _logger.LogDebug("Connection {ConnectionId} disconnected", connectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove connection {ConnectionId}", connectionId);
                // Don't throw - disconnection cleanup failure shouldn't break the disconnection
            }
        }

        public async Task RecordActivityAsync(string connectionId)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.LastActivityAt = DateTime.UtcNow;
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                        await RefreshConnectionFieldExpirationAsync(connectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record activity for connection {ConnectionId}", connectionId);
            }
        }

        public async Task RecordMessageSentAsync(string connectionId)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.MessagesSent++;
                        connectionInfo.LastActivityAt = DateTime.UtcNow;
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                        await RefreshConnectionFieldExpirationAsync(connectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record message sent for connection {ConnectionId}", connectionId);
            }
        }

        public async Task RecordMessageAcknowledgedAsync(string connectionId)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.MessagesAcknowledged++;
                        connectionInfo.LastActivityAt = DateTime.UtcNow;
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                        await RefreshConnectionFieldExpirationAsync(connectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record message acknowledged for connection {ConnectionId}", connectionId);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }

        internal static bool SupportsHashFieldExpiration(Version serverVersion, bool enabled)
        {
            return enabled && serverVersion >= RedisHashFieldExpirationMinimumVersion;
        }

        private async Task RefreshConnectionFieldExpirationAsync(string connectionId)
        {
            if (!_hashFieldExpirationSupported || _redis == null)
            {
                return;
            }

            try
            {
                await _redis.HashFieldExpireAsync(
                    _connectionsKey,
                    [connectionId],
                    _connectionFieldTtl,
                    ExpireWhen.Always);
            }
            catch (RedisServerException ex) when (
                ex.Message.Contains("unknown command", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("syntax", StringComparison.OrdinalIgnoreCase))
            {
                _hashFieldExpirationSupported = false;
                _logger.LogWarning(
                    ex,
                    "Redis hash field expiration is not supported by this server; continuing with periodic cleanup");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to refresh expiration for SignalR connection {ConnectionId}; periodic cleanup remains active",
                    connectionId);
            }
        }
    }
}
