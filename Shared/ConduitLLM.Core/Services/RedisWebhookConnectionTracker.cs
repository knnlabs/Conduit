using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Interface for tracking webhook connections across instances
    /// </summary>
    public interface IWebhookConnectionTracker
    {
        /// <summary>
        /// Adds webhooks to a connection's tracking list
        /// </summary>
        Task AddWebhooksToConnectionAsync(string connectionId, IEnumerable<string> webhookUrls);
        
        /// <summary>
        /// Removes webhooks from a connection's tracking list
        /// </summary>
        Task RemoveWebhooksFromConnectionAsync(string connectionId, IEnumerable<string> webhookUrls);
        
        /// <summary>
        /// Gets all webhooks tracked by a connection
        /// </summary>
        Task<HashSet<string>> GetConnectionWebhooksAsync(string connectionId);
        
        /// <summary>
        /// Gets all connections tracking a specific webhook
        /// </summary>
        Task<HashSet<string>> GetWebhookConnectionsAsync(string webhookUrl);
        
        /// <summary>
        /// Removes all webhooks for a connection (on disconnect)
        /// </summary>
        Task RemoveConnectionAsync(string connectionId);
        
        /// <summary>
        /// Gets count of connections tracking a webhook
        /// </summary>
        Task<int> GetWebhookConnectionCountAsync(string webhookUrl);
    }
    
    /// <summary>
    /// Redis-based implementation of webhook connection tracking
    /// Allows distributed tracking of which connections are monitoring which webhooks
    /// </summary>
    public class RedisWebhookConnectionTracker : IWebhookConnectionTracker
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisWebhookConnectionTracker> _logger;
        
        private const string CONNECTION_WEBHOOKS_KEY = "webhook:connections:{0}:webhooks";
        private const string WEBHOOK_CONNECTIONS_KEY = "webhook:webhooks:{0}:connections";
        private const string CONNECTION_TIMESTAMP_KEY = "webhook:connections:{0}:timestamp";
        private const int CONNECTION_EXPIRY_HOURS = 24;
        
        public RedisWebhookConnectionTracker(
            IConnectionMultiplexer redis,
            ILogger<RedisWebhookConnectionTracker> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task AddWebhooksToConnectionAsync(string connectionId, IEnumerable<string> webhookUrls)
        {
            try
            {
                var db = _redis.GetDatabase();
                var transaction = db.CreateTransaction();
                
                var connectionKey = string.Format(CONNECTION_WEBHOOKS_KEY, connectionId);
                var timestampKey = string.Format(CONNECTION_TIMESTAMP_KEY, connectionId);
                
                foreach (var webhookUrl in webhookUrls)
                {
                    // Add webhook to connection's set
                    _ = transaction.SetAddAsync(connectionKey, webhookUrl);
                    
                    // Add connection to webhook's set
                    var webhookKey = string.Format(WEBHOOK_CONNECTIONS_KEY, GetUrlHash(webhookUrl));
                    _ = transaction.SetAddAsync(webhookKey, connectionId);
                    _ = transaction.KeyExpireAsync(webhookKey, TimeSpan.FromHours(CONNECTION_EXPIRY_HOURS));
                }
                
                // Update connection timestamp
                _ = transaction.StringSetAsync(timestampKey, DateTime.UtcNow.ToString("O"), 
                    TimeSpan.FromHours(CONNECTION_EXPIRY_HOURS));
                _ = transaction.KeyExpireAsync(connectionKey, TimeSpan.FromHours(CONNECTION_EXPIRY_HOURS));
                
                await transaction.ExecuteAsync();
                
                _logger.LogDebug("Added {Count} webhooks to connection {ConnectionId}", 
                    webhookUrls.Count(), connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding webhooks to connection {ConnectionId}", connectionId);
            }
        }
        
        public async Task RemoveWebhooksFromConnectionAsync(string connectionId, IEnumerable<string> webhookUrls)
        {
            try
            {
                var db = _redis.GetDatabase();
                var transaction = db.CreateTransaction();
                
                var connectionKey = string.Format(CONNECTION_WEBHOOKS_KEY, connectionId);
                
                foreach (var webhookUrl in webhookUrls)
                {
                    // Remove webhook from connection's set
                    _ = transaction.SetRemoveAsync(connectionKey, webhookUrl);
                    
                    // Remove connection from webhook's set
                    var webhookKey = string.Format(WEBHOOK_CONNECTIONS_KEY, GetUrlHash(webhookUrl));
                    _ = transaction.SetRemoveAsync(webhookKey, connectionId);
                }
                
                await transaction.ExecuteAsync();
                
                _logger.LogDebug("Removed {Count} webhooks from connection {ConnectionId}", 
                    webhookUrls.Count(), connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing webhooks from connection {ConnectionId}", connectionId);
            }
        }
        
        public async Task<HashSet<string>> GetConnectionWebhooksAsync(string connectionId)
        {
            try
            {
                var db = _redis.GetDatabase();
                var connectionKey = string.Format(CONNECTION_WEBHOOKS_KEY, connectionId);
                var webhooks = await db.SetMembersAsync(connectionKey);
                
                return webhooks.Select(w => w.ToString()).ToHashSet();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting webhooks for connection {ConnectionId}", connectionId);
                return new HashSet<string>();
            }
        }
        
        public async Task<HashSet<string>> GetWebhookConnectionsAsync(string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var webhookKey = string.Format(WEBHOOK_CONNECTIONS_KEY, GetUrlHash(webhookUrl));
                var connections = await db.SetMembersAsync(webhookKey);
                
                return connections.Select(c => c.ToString()).ToHashSet();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connections for webhook {WebhookUrl}", webhookUrl);
                return new HashSet<string>();
            }
        }
        
        public async Task RemoveConnectionAsync(string connectionId)
        {
            try
            {
                var db = _redis.GetDatabase();
                
                // Get all webhooks for this connection
                var connectionKey = string.Format(CONNECTION_WEBHOOKS_KEY, connectionId);
                var webhooks = await db.SetMembersAsync(connectionKey);
                
                if (webhooks.Length > 0)
                {
                    var transaction = db.CreateTransaction();
                    
                    // Remove connection from all webhook sets
                    foreach (var webhook in webhooks)
                    {
                        var webhookKey = string.Format(WEBHOOK_CONNECTIONS_KEY, 
                            GetUrlHash(webhook.ToString()));
                        _ = transaction.SetRemoveAsync(webhookKey, connectionId);
                    }
                    
                    // Remove connection's webhook set
                    _ = transaction.KeyDeleteAsync(connectionKey);
                    
                    // Remove connection timestamp
                    var timestampKey = string.Format(CONNECTION_TIMESTAMP_KEY, connectionId);
                    _ = transaction.KeyDeleteAsync(timestampKey);
                    
                    await transaction.ExecuteAsync();
                }
                
                _logger.LogDebug("Removed connection {ConnectionId} and its {Count} webhook subscriptions", 
                    connectionId, webhooks.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing connection {ConnectionId}", connectionId);
            }
        }
        
        public async Task<int> GetWebhookConnectionCountAsync(string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var webhookKey = string.Format(WEBHOOK_CONNECTIONS_KEY, GetUrlHash(webhookUrl));
                var count = await db.SetLengthAsync(webhookKey);
                
                return (int)count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection count for webhook {WebhookUrl}", webhookUrl);
                return 0;
            }
        }
        
        private string GetUrlHash(string webhookUrl)
        {
            // Create a consistent hash for the URL to use as Redis key component
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(webhookUrl));
            return Convert.ToBase64String(hashBytes).Replace("/", "-").Replace("+", "_").Substring(0, 16);
        }
    }
    
    /// <summary>
    /// In-memory fallback implementation of webhook connection tracking
    /// Used when Redis is not available
    /// </summary>
    public class InMemoryWebhookConnectionTracker : IWebhookConnectionTracker
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _connectionWebhooks = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _webhookConnections = new();
        private readonly ILogger<InMemoryWebhookConnectionTracker> _logger;
        
        public InMemoryWebhookConnectionTracker(ILogger<InMemoryWebhookConnectionTracker> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public Task AddWebhooksToConnectionAsync(string connectionId, IEnumerable<string> webhookUrls)
        {
            var connectionSet = _connectionWebhooks.GetOrAdd(connectionId, _ => new HashSet<string>());
            
            lock (connectionSet)
            {
                foreach (var webhookUrl in webhookUrls)
                {
                    connectionSet.Add(webhookUrl);
                    
                    var webhookSet = _webhookConnections.GetOrAdd(webhookUrl, _ => new HashSet<string>());
                    lock (webhookSet)
                    {
                        webhookSet.Add(connectionId);
                    }
                }
            }
            
            _logger.LogDebug("Added {Count} webhooks to connection {ConnectionId} (in-memory)", 
                webhookUrls.Count(), connectionId);
            
            return Task.CompletedTask;
        }
        
        public Task RemoveWebhooksFromConnectionAsync(string connectionId, IEnumerable<string> webhookUrls)
        {
            if (_connectionWebhooks.TryGetValue(connectionId, out var connectionSet))
            {
                lock (connectionSet)
                {
                    foreach (var webhookUrl in webhookUrls)
                    {
                        connectionSet.Remove(webhookUrl);
                        
                        if (_webhookConnections.TryGetValue(webhookUrl, out var webhookSet))
                        {
                            lock (webhookSet)
                            {
                                webhookSet.Remove(connectionId);
                            }
                        }
                    }
                }
            }
            
            return Task.CompletedTask;
        }
        
        public Task<HashSet<string>> GetConnectionWebhooksAsync(string connectionId)
        {
            if (_connectionWebhooks.TryGetValue(connectionId, out var webhooks))
            {
                lock (webhooks)
                {
                    return Task.FromResult(new HashSet<string>(webhooks));
                }
            }
            
            return Task.FromResult(new HashSet<string>());
        }
        
        public Task<HashSet<string>> GetWebhookConnectionsAsync(string webhookUrl)
        {
            if (_webhookConnections.TryGetValue(webhookUrl, out var connections))
            {
                lock (connections)
                {
                    return Task.FromResult(new HashSet<string>(connections));
                }
            }
            
            return Task.FromResult(new HashSet<string>());
        }
        
        public Task RemoveConnectionAsync(string connectionId)
        {
            if (_connectionWebhooks.TryRemove(connectionId, out var webhooks))
            {
                foreach (var webhook in webhooks)
                {
                    if (_webhookConnections.TryGetValue(webhook, out var connections))
                    {
                        lock (connections)
                        {
                            connections.Remove(connectionId);
                        }
                    }
                }
                
                _logger.LogDebug("Removed connection {ConnectionId} with {Count} webhooks (in-memory)", 
                    connectionId, webhooks.Count);
            }
            
            return Task.CompletedTask;
        }
        
        public Task<int> GetWebhookConnectionCountAsync(string webhookUrl)
        {
            if (_webhookConnections.TryGetValue(webhookUrl, out var connections))
            {
                lock (connections)
                {
                    return Task.FromResult(connections.Count);
                }
            }
            
            return Task.FromResult(0);
        }
    }
}