using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Prometheus;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Distributed SignalR metrics service with centralized connection tracking across instances
    /// </summary>
    public class DistributedSignalRMetricsService : IDistributedSignalRMetricsService, IHostedService, IDisposable
    {
        private readonly IDatabase _database;
        private readonly ILogger<DistributedSignalRMetricsService> _logger;
        private readonly SignalRConnectionOptions _connectionOptions;

        public string InstanceId { get; }

        // Redis keys
        private const string ActiveConnectionsPrefix = "signalr_connections";
        private const string InstancesSetKey = "signalr_metrics_instances";
        private const string ConnectionEventsStreamKey = "signalr_events_stream";
        private const string MetricsAggregatesKey = "signalr_metrics_aggregates";
        private const string VirtualKeyConnectionsPrefix = "signalr_vk_connections";
        
        private Timer? _heartbeatTimer;
        private Timer? _metricsTimer;
        private Timer? _cleanupTimer;

        // Prometheus metrics (updated to track distributed data)
        private static readonly Gauge ActiveConnections = Prometheus.Metrics
            .CreateGauge("conduit_signalr_connections_active_distributed", "Number of active SignalR connections across all instances",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "hub", "virtual_key_id" }
                });

        private static readonly Counter ConnectionsTotal = Prometheus.Metrics
            .CreateCounter("conduit_signalr_connections_total_distributed", "Total number of SignalR connections across all instances",
                new CounterConfiguration
                {
                    LabelNames = new[] { "hub", "status", "instance_id" }
                });

        private static readonly Histogram ConnectionDuration = Prometheus.Metrics
            .CreateHistogram("conduit_signalr_connection_duration_seconds_distributed", "SignalR connection duration in seconds",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "hub", "instance_id" },
                    Buckets = Histogram.ExponentialBuckets(1, 2, 16)
                });

        private static readonly Counter MessagesTotal = Prometheus.Metrics
            .CreateCounter("conduit_signalr_messages_total_distributed", "Total number of SignalR messages across all instances",
                new CounterConfiguration
                {
                    LabelNames = new[] { "hub", "method", "direction", "instance_id" }
                });

        private static readonly Gauge GlobalConnectionCount = Prometheus.Metrics
            .CreateGauge("conduit_signalr_global_connections", "Total active SignalR connections across all instances");

        private static readonly Gauge VirtualKeyConnectionCount = Prometheus.Metrics
            .CreateGauge("conduit_signalr_virtual_key_connections", "Active SignalR connections per virtual key",
                new GaugeConfiguration
                {
                    LabelNames = new[] { "virtual_key_id" }
                });

        public DistributedSignalRMetricsService(
            IConnectionMultiplexer redis,
            ILogger<DistributedSignalRMetricsService> logger,
            IOptions<SignalRConnectionOptions> connectionOptions)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            _connectionOptions = connectionOptions?.Value ?? new SignalRConnectionOptions();
            InstanceId = Environment.MachineName + "_" + Environment.ProcessId + "_" + Guid.NewGuid().ToString("N")[..8];
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await RegisterInstanceAsync();
            
            _logger.LogInformation("Distributed SignalR metrics service started with instance ID: {InstanceId}", InstanceId);

            // Start heartbeat timer (every 30 seconds)
            _heartbeatTimer = new Timer(
                async _ => await UpdateHeartbeatAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));

            // Start metrics calculation timer (every 30 seconds)
            _metricsTimer = new Timer(
                async _ => await CalculateDistributedMetricsAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));

            // Start cleanup timer (every 5 minutes)
            _cleanupTimer = new Timer(
                async _ => await CleanupStaleConnectionsAsync(),
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Distributed SignalR metrics service stopping...");

            _heartbeatTimer?.Change(Timeout.Infinite, 0);
            _metricsTimer?.Change(Timeout.Infinite, 0);
            _cleanupTimer?.Change(Timeout.Infinite, 0);

            await UnregisterInstanceAsync();
        }

        public async Task RegisterInstanceAsync()
        {
            var instanceData = new
            {
                InstanceId,
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                StartedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "data", JsonSerializer.Serialize(instanceData));
            await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
        }

        public async Task UnregisterInstanceAsync()
        {
            // Clean up all connections for this instance
            await CleanupInstanceConnectionsAsync();
            await _database.KeyDeleteAsync($"{InstancesSetKey}:{InstanceId}");
        }

        public async Task UpdateHeartbeatAsync()
        {
            try
            {
                await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "last_heartbeat", DateTime.UtcNow.Ticks);
                await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update heartbeat for instance {InstanceId}", InstanceId);
            }
        }

        public async Task OnConnectedAsync(string connectionId, string hubName, string virtualKeyId)
        {
            var connectionInfo = new
            {
                ConnectionId = connectionId,
                HubName = hubName,
                VirtualKeyId = virtualKeyId,
                InstanceId,
                ConnectedAt = DateTime.UtcNow.Ticks,
                LastActivity = DateTime.UtcNow.Ticks
            };

            var connectionKey = $"{ActiveConnectionsPrefix}:{connectionId}";
            await _database.StringSetAsync(connectionKey, JsonSerializer.Serialize(connectionInfo), TimeSpan.FromHours(24));

            // Add to virtual key connections set
            var vkConnectionsKey = $"{VirtualKeyConnectionsPrefix}:{virtualKeyId}";
            await _database.SetAddAsync(vkConnectionsKey, connectionId);
            await _database.KeyExpireAsync(vkConnectionsKey, TimeSpan.FromHours(24));

            // Add to connection events stream
            await _database.StreamAddAsync(ConnectionEventsStreamKey, "connected", JsonSerializer.Serialize(connectionInfo));
            await _database.StreamTrimAsync(ConnectionEventsStreamKey, 10000, false);

            // Update Prometheus metrics
            ConnectionsTotal.WithLabels(hubName, "connected", InstanceId).Inc();

            _logger.LogDebug("SignalR connection {ConnectionId} connected to hub {HubName} from instance {InstanceId}", 
                connectionId, hubName, InstanceId);
        }

        public async Task OnDisconnectedAsync(string connectionId, string? exception = null)
        {
            var connectionKey = $"{ActiveConnectionsPrefix}:{connectionId}";
            var connectionData = await _database.StringGetAsync(connectionKey);
            
            if (!connectionData.HasValue)
            {
                _logger.LogWarning("Connection {ConnectionId} not found during disconnect", connectionId);
                return;
            }

            var connectionInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(connectionData.ToString());
            if (connectionInfo == null) return;

            var hubName = connectionInfo.GetValueOrDefault("HubName", "")?.ToString() ?? "";
            var virtualKeyId = connectionInfo.GetValueOrDefault("VirtualKeyId", "")?.ToString() ?? "";
            var connectedAtTicks = long.Parse(connectionInfo.GetValueOrDefault("ConnectedAt", "0")?.ToString() ?? "0");
            var connectedAt = new DateTime(connectedAtTicks);

            var duration = (DateTime.UtcNow - connectedAt).TotalSeconds;
            var status = string.IsNullOrEmpty(exception) ? "disconnected" : "failed";

            // Remove connection
            await _database.KeyDeleteAsync(connectionKey);

            // Remove from virtual key connections set
            var vkConnectionsKey = $"{VirtualKeyConnectionsPrefix}:{virtualKeyId}";
            await _database.SetRemoveAsync(vkConnectionsKey, connectionId);

            // Add to events stream
            var disconnectInfo = new
            {
                ConnectionId = connectionId,
                HubName = hubName,
                VirtualKeyId = virtualKeyId,
                InstanceId,
                DisconnectedAt = DateTime.UtcNow.Ticks,
                Duration = duration,
                Exception = exception
            };

            await _database.StreamAddAsync(ConnectionEventsStreamKey, "disconnected", JsonSerializer.Serialize(disconnectInfo));

            // Update Prometheus metrics
            ConnectionsTotal.WithLabels(hubName ?? "", status, InstanceId).Inc();
            ConnectionDuration.WithLabels(hubName ?? "", InstanceId).Observe(duration);

            _logger.LogDebug("SignalR connection {ConnectionId} disconnected from hub {HubName} after {Duration:F2}s from instance {InstanceId}",
                connectionId, hubName, duration, InstanceId);
        }

        public async Task OnReconnectedAsync(string connectionId, string hubName)
        {
            var connectionKey = $"{ActiveConnectionsPrefix}:{connectionId}";
            var connectionData = await _database.StringGetAsync(connectionKey);
            
            if (connectionData.HasValue)
            {
                var connectionInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(connectionData.ToString());
                if (connectionInfo != null)
                {
                    connectionInfo["LastActivity"] = DateTime.UtcNow.Ticks;
                    await _database.StringSetAsync(connectionKey, JsonSerializer.Serialize(connectionInfo), TimeSpan.FromHours(24));
                }
            }

            // Add to events stream
            var reconnectInfo = new
            {
                ConnectionId = connectionId,
                HubName = hubName,
                InstanceId,
                ReconnectedAt = DateTime.UtcNow.Ticks
            };

            await _database.StreamAddAsync(ConnectionEventsStreamKey, "reconnected", JsonSerializer.Serialize(reconnectInfo));

            _logger.LogDebug("SignalR connection {ConnectionId} reconnected to hub {HubName} on instance {InstanceId}", 
                connectionId, hubName, InstanceId);
        }

        public async Task OnMessageSentAsync(string hubName, string method, double processingTimeMs = 0)
        {
            MessagesTotal.WithLabels(hubName, method, "sent", InstanceId).Inc();
            
            // Store aggregated message metrics
            var messageInfo = new
            {
                HubName = hubName,
                Method = method,
                Direction = "sent",
                InstanceId,
                ProcessingTimeMs = processingTimeMs,
                Timestamp = DateTime.UtcNow.Ticks
            };

            await _database.StreamAddAsync($"messages_stream_{hubName}", "sent", JsonSerializer.Serialize(messageInfo));
        }

        public async Task OnMessageReceivedAsync(string hubName, string method)
        {
            MessagesTotal.WithLabels(hubName, method, "received", InstanceId).Inc();
            
            var messageInfo = new
            {
                HubName = hubName,
                Method = method,
                Direction = "received",
                InstanceId,
                Timestamp = DateTime.UtcNow.Ticks
            };

            await _database.StreamAddAsync($"messages_stream_{hubName}", "received", JsonSerializer.Serialize(messageInfo));
        }

        public async Task OnTaskSubscribedAsync(string hubName, string taskType)
        {
            var subscriptionInfo = new
            {
                HubName = hubName,
                TaskType = taskType,
                InstanceId,
                Action = "subscribed",
                Timestamp = DateTime.UtcNow.Ticks
            };

            await _database.StreamAddAsync($"subscriptions_stream_{hubName}", "subscribed", JsonSerializer.Serialize(subscriptionInfo));
        }

        public async Task OnTaskUnsubscribedAsync(string hubName, string taskType)
        {
            var subscriptionInfo = new
            {
                HubName = hubName,
                TaskType = taskType,
                InstanceId,
                Action = "unsubscribed",
                Timestamp = DateTime.UtcNow.Ticks
            };

            await _database.StreamAddAsync($"subscriptions_stream_{hubName}", "unsubscribed", JsonSerializer.Serialize(subscriptionInfo));
        }

        public async Task<int> GetGlobalConnectionCountAsync()
        {
            var pattern = $"{ActiveConnectionsPrefix}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);
            
            var count = 0;
            foreach (var key in keys)
            {
                if (await _database.KeyExistsAsync(key))
                    count++;
            }
            
            return count;
        }

        public async Task<int> GetConnectionCountForVirtualKeyAsync(string virtualKeyId)
        {
            var vkConnectionsKey = $"{VirtualKeyConnectionsPrefix}:{virtualKeyId}";
            return (int)await _database.SetLengthAsync(vkConnectionsKey);
        }

        public async Task<bool> IsConnectionLimitReachedAsync(string virtualKeyId)
        {
            var count = await GetConnectionCountForVirtualKeyAsync(virtualKeyId);
            return count >= _connectionOptions.MaxConnectionsPerVirtualKey;
        }

        public async Task<bool> IsGlobalConnectionLimitReachedAsync()
        {
            var count = await GetGlobalConnectionCountAsync();
            return count >= _connectionOptions.MaxTotalConnections;
        }

        public async Task<Dictionary<string, object>> GetAggregatedMetricsAsync()
        {
            var globalConnections = await GetGlobalConnectionCountAsync();
            var activeInstances = await GetActiveInstancesAsync();
            
            // Get connection distribution by hub
            var hubDistribution = await GetConnectionDistributionByHubAsync();
            
            // Get recent connection events (last 5 minutes)
            var recentEvents = await GetRecentConnectionEventsAsync(TimeSpan.FromMinutes(5));
            
            return new Dictionary<string, object>
            {
                ["globalConnections"] = globalConnections,
                ["activeInstances"] = activeInstances.Count,
                ["hubDistribution"] = hubDistribution,
                ["recentEvents"] = recentEvents,
                ["connectionLimits"] = new
                {
                    MaxPerVirtualKey = _connectionOptions.MaxConnectionsPerVirtualKey,
                    MaxGlobal = _connectionOptions.MaxTotalConnections,
                    GlobalUtilization = (double)globalConnections / _connectionOptions.MaxTotalConnections * 100
                },
                ["timestamp"] = DateTime.UtcNow
            };
        }

        public async Task<List<string>> GetActiveInstancesAsync()
        {
            var pattern = $"{InstancesSetKey}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);
            
            var instances = new List<string>();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-1);
            
            foreach (var key in keys)
            {
                var lastHeartbeat = await _database.HashGetAsync(key, "last_heartbeat");
                if (lastHeartbeat.HasValue)
                {
                    var heartbeatTime = new DateTime((long)lastHeartbeat);
                    if (heartbeatTime > cutoffTime)
                    {
                        var instanceId = key.ToString().Split(':').Last();
                        instances.Add(instanceId);
                    }
                }
            }
            
            return instances;
        }

        private async Task CalculateDistributedMetricsAsync()
        {
            try
            {
                // Update global connection count
                var globalConnections = await GetGlobalConnectionCountAsync();
                GlobalConnectionCount.Set(globalConnections);

                // Update virtual key connection counts
                await UpdateVirtualKeyMetricsAsync();

                // Update hub-specific metrics
                await UpdateHubMetricsAsync();

                // Log warnings if approaching limits
                if (globalConnections > _connectionOptions.MaxTotalConnections * 0.8)
                {
                    _logger.LogWarning("SignalR connections approaching global limit: {Count}/{Max} ({Percentage:F1}%)",
                        globalConnections, _connectionOptions.MaxTotalConnections, (double)globalConnections / _connectionOptions.MaxTotalConnections * 100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating distributed SignalR metrics");
            }
        }

        private async Task UpdateVirtualKeyMetricsAsync()
        {
            var pattern = $"{VirtualKeyConnectionsPrefix}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);

            foreach (var key in keys)
            {
                var virtualKeyId = key.ToString().Split(':').Last();
                var connectionCount = await _database.SetLengthAsync(key);
                
                VirtualKeyConnectionCount.WithLabels(virtualKeyId).Set(connectionCount);

                // Log warning if virtual key is approaching limit
                if (connectionCount > _connectionOptions.MaxConnectionsPerVirtualKey * 0.8)
                {
                    _logger.LogWarning("Virtual key {VirtualKeyId} approaching connection limit: {Count}/{Max}",
                        virtualKeyId, connectionCount, _connectionOptions.MaxConnectionsPerVirtualKey);
                }
            }
        }

        private async Task UpdateHubMetricsAsync()
        {
            var hubCounts = await GetConnectionDistributionByHubAsync();
            
            foreach (var hub in hubCounts)
            {
                // Update ActiveConnections metric for each hub
                // Note: We aggregate across all virtual keys for the hub total
                var totalForHub = hub.Value.Values.Sum();
                
                foreach (var vk in hub.Value)
                {
                    ActiveConnections.WithLabels(hub.Key, vk.Key).Set(vk.Value);
                }
            }
        }

        private async Task<Dictionary<string, Dictionary<string, int>>> GetConnectionDistributionByHubAsync()
        {
            var distribution = new Dictionary<string, Dictionary<string, int>>();
            var pattern = $"{ActiveConnectionsPrefix}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);

            foreach (var key in keys)
            {
                var connectionData = await _database.StringGetAsync(key);
                if (!connectionData.HasValue) continue;

                try
                {
                    var connectionInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(connectionData.ToString());
                    if (connectionInfo == null) continue;

                    var hubName = connectionInfo.GetValueOrDefault("HubName", "").ToString();
                    var virtualKeyId = connectionInfo.GetValueOrDefault("VirtualKeyId", "").ToString();

                    if (string.IsNullOrEmpty(hubName) || string.IsNullOrEmpty(virtualKeyId)) continue;

                    if (!distribution.ContainsKey(hubName))
                    {
                        distribution[hubName] = new Dictionary<string, int>();
                    }

                    distribution[hubName][virtualKeyId] = distribution[hubName].GetValueOrDefault(virtualKeyId, 0) + 1;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse connection info for key {Key}", LoggingSanitizer.S(key));
                }
            }

            return distribution;
        }

        private async Task<List<object>> GetRecentConnectionEventsAsync(TimeSpan timeWindow)
        {
            var startTime = DateTime.UtcNow - timeWindow;
            var events = await _database.StreamRangeAsync(ConnectionEventsStreamKey, $"{startTime.Ticks}", "+", 1000);
            
            var recentEvents = new List<object>();
            foreach (var entry in events)
            {
                var eventType = entry.Values[0].Name;
                var eventData = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Values[0].Value.ToString());
                
                recentEvents.Add(new
                {
                    Type = eventType,
                    Timestamp = entry.Id,
                    Data = eventData
                });
            }
            
            return recentEvents;
        }

        private async Task CleanupStaleConnectionsAsync()
        {
            try
            {
                var staleThreshold = DateTime.UtcNow.AddMinutes(-10); // 10 minutes without activity
                var pattern = $"{ActiveConnectionsPrefix}:*";
                var server = _database.Multiplexer.GetPrimaryServer();
                var keys = server.Keys(pattern: pattern);

                var staleConnections = new List<string>();

                foreach (var key in keys)
                {
                    var connectionData = await _database.StringGetAsync(key);
                    if (!connectionData.HasValue) continue;

                    try
                    {
                        var connectionInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(connectionData.ToString());
                        if (connectionInfo == null) continue;

                        var lastActivityTicks = long.Parse(connectionInfo.GetValueOrDefault("LastActivity", "0")?.ToString() ?? "0");
                        var lastActivity = new DateTime(lastActivityTicks);

                        if (lastActivity < staleThreshold)
                        {
                            var connectionId = connectionInfo.GetValueOrDefault("ConnectionId", "")?.ToString();
                            if (!string.IsNullOrEmpty(connectionId))
                            {
                                staleConnections.Add(connectionId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check connection staleness for key {Key}", key);
                    }
                }

                foreach (var connectionId in staleConnections)
                {
                    _logger.LogWarning("Removing stale SignalR connection {ConnectionId}", connectionId);
                    await OnDisconnectedAsync(connectionId, "Stale connection removed by cleanup");
                }

                if (staleConnections.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} stale SignalR connections", staleConnections.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stale connection cleanup");
            }
        }

        private async Task CleanupInstanceConnectionsAsync()
        {
            try
            {
                // Find all connections for this instance and clean them up
                var pattern = $"{ActiveConnectionsPrefix}:*";
                var server = _database.Multiplexer.GetPrimaryServer();
                var keys = server.Keys(pattern: pattern);

                var instanceConnections = new List<string>();

                foreach (var key in keys)
                {
                    var connectionData = await _database.StringGetAsync(key);
                    if (!connectionData.HasValue) continue;

                    try
                    {
                        var connectionInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(connectionData.ToString());
                        if (connectionInfo == null) continue;

                        var instanceId = connectionInfo.GetValueOrDefault("InstanceId", "").ToString();
                        if (instanceId == InstanceId)
                        {
                            var connectionId = connectionInfo.GetValueOrDefault("ConnectionId", "")?.ToString();
                            if (!string.IsNullOrEmpty(connectionId))
                            {
                                instanceConnections.Add(connectionId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check connection instance for key {Key}", key);
                    }
                }

                foreach (var connectionId in instanceConnections)
                {
                    await OnDisconnectedAsync(connectionId, "Instance shutdown cleanup");
                }

                if (instanceConnections.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} connections for instance {InstanceId}", 
                        instanceConnections.Count, InstanceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up instance connections");
            }
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _metricsTimer?.Dispose();
            _cleanupTimer?.Dispose();
        }
    }
}