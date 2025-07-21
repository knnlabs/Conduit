using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.Authentication
{
    /// <summary>
    /// Hub filter that applies rate limiting to SignalR connections based on virtual keys
    /// </summary>
    public class VirtualKeySignalRRateLimitFilter : IHubFilter
    {
        private readonly VirtualKeyRateLimitCache _rateLimitCache;
        private readonly ILogger<VirtualKeySignalRRateLimitFilter> _logger;
        
        // Track connection counts and request times per virtual key
        private readonly ConcurrentDictionary<string, ConnectionRateLimitInfo> _connectionInfo;
        
        private class ConnectionRateLimitInfo
        {
            public int ActiveConnections { get; set; }
            public DateTime LastMinuteStart { get; set; }
            public int RequestsThisMinute { get; set; }
            public DateTime LastDayStart { get; set; }
            public int RequestsToday { get; set; }
            public readonly object Lock = new object();
        }

        /// <summary>
        /// Initializes a new instance of VirtualKeySignalRRateLimitFilter
        /// </summary>
        public VirtualKeySignalRRateLimitFilter(
            VirtualKeyRateLimitCache rateLimitCache,
            ILogger<VirtualKeySignalRRateLimitFilter> logger)
        {
            _rateLimitCache = rateLimitCache;
            _logger = logger;
            _connectionInfo = new ConcurrentDictionary<string, ConnectionRateLimitInfo>();
        }

        /// <summary>
        /// Called when a hub method is invoked
        /// </summary>
        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            var virtualKeyHash = GetVirtualKeyHash(invocationContext.Context);
            
            if (string.IsNullOrEmpty(virtualKeyHash))
            {
                // No virtual key, allow through (authentication should have caught this)
                return await next(invocationContext);
            }

            // Check rate limits
            var rateLimits = _rateLimitCache.GetRateLimits(virtualKeyHash);
            if (rateLimits == null || (!rateLimits.RateLimitRpm.HasValue && !rateLimits.RateLimitRpd.HasValue))
            {
                // No rate limits configured
                return await next(invocationContext);
            }

            // Get or create rate limit info
            var info = _connectionInfo.GetOrAdd(virtualKeyHash, _ => new ConnectionRateLimitInfo
            {
                LastMinuteStart = DateTime.UtcNow,
                LastDayStart = DateTime.UtcNow.Date
            });

            lock (info.Lock)
            {
                var now = DateTime.UtcNow;
                
                // Reset minute counter if needed
                if (now - info.LastMinuteStart >= TimeSpan.FromMinutes(1))
                {
                    info.LastMinuteStart = now;
                    info.RequestsThisMinute = 0;
                }
                
                // Reset daily counter if needed
                if (now.Date != info.LastDayStart)
                {
                    info.LastDayStart = now.Date;
                    info.RequestsToday = 0;
                }
                
                // Check minute limit
                if (rateLimits.RateLimitRpm.HasValue && info.RequestsThisMinute >= rateLimits.RateLimitRpm.Value)
                {
                    _logger.LogWarning("Virtual Key {KeyHash} exceeded RPM limit of {Limit} for SignalR method {Method}",
                        virtualKeyHash, rateLimits.RateLimitRpm.Value, invocationContext.HubMethodName);
                    throw new HubException("Rate limit exceeded. Please try again later.");
                }
                
                // Check daily limit
                if (rateLimits.RateLimitRpd.HasValue && info.RequestsToday >= rateLimits.RateLimitRpd.Value)
                {
                    _logger.LogWarning("Virtual Key {KeyHash} exceeded RPD limit of {Limit} for SignalR method {Method}",
                        virtualKeyHash, rateLimits.RateLimitRpd.Value, invocationContext.HubMethodName);
                    throw new HubException("Daily rate limit exceeded. Please try again tomorrow.");
                }
                
                // Increment counters
                info.RequestsThisMinute++;
                info.RequestsToday++;
            }

            return await next(invocationContext);
        }

        /// <summary>
        /// Called when a client connects
        /// </summary>
        public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            var virtualKeyHash = GetVirtualKeyHash(context.Context);
            
            if (!string.IsNullOrEmpty(virtualKeyHash))
            {
                var info = _connectionInfo.GetOrAdd(virtualKeyHash, _ => new ConnectionRateLimitInfo
                {
                    LastMinuteStart = DateTime.UtcNow,
                    LastDayStart = DateTime.UtcNow.Date
                });
                
                lock (info.Lock)
                {
                    info.ActiveConnections++;
                }
                
                _logger.LogDebug("Virtual Key {KeyHash} connected. Active connections: {Count}",
                    virtualKeyHash, info.ActiveConnections);
            }

            await next(context);
        }

        /// <summary>
        /// Called when a client disconnects
        /// </summary>
        public async Task OnDisconnectedAsync(
            HubLifetimeContext context,
            Exception? exception,
            Func<HubLifetimeContext, Exception?, Task> next)
        {
            var virtualKeyHash = GetVirtualKeyHash(context.Context);
            
            if (!string.IsNullOrEmpty(virtualKeyHash))
            {
                if (_connectionInfo.TryGetValue(virtualKeyHash, out var info))
                {
                    lock (info.Lock)
                    {
                        info.ActiveConnections = Math.Max(0, info.ActiveConnections - 1);
                        
                        // Clean up if no active connections and no recent activity
                        if (info.ActiveConnections == 0 && 
                            DateTime.UtcNow - info.LastMinuteStart > TimeSpan.FromMinutes(5))
                        {
                            _connectionInfo.TryRemove(virtualKeyHash, out _);
                        }
                    }
                    
                    _logger.LogDebug("Virtual Key {KeyHash} disconnected. Active connections: {Count}",
                        virtualKeyHash, info.ActiveConnections);
                }
            }

            await next(context, exception);
        }

        /// <summary>
        /// Gets the virtual key hash from the connection context
        /// </summary>
        private string? GetVirtualKeyHash(HubCallerContext context)
        {
            // Try from Items first (set by hub filter)
            if (context.Items.TryGetValue("VirtualKeyHash", out var itemValue) && itemValue is string itemHash)
            {
                return itemHash;
            }
            
            // Try from User claims (set by authentication handler)
            var claim = context.User?.FindFirst("VirtualKeyHash");
            return claim?.Value;
        }
    }
}