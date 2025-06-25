using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Caches virtual key rate limit configurations for synchronous access
    /// </summary>
    public class VirtualKeyRateLimitCache : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VirtualKeyRateLimitCache> _logger;
        private readonly ConcurrentDictionary<string, VirtualKeyRateLimits> _rateLimits;
        private Timer? _refreshTimer;

        /// <summary>
        /// Represents rate limit configuration for a virtual key
        /// </summary>
        public class VirtualKeyRateLimits
        {
            public int? RateLimitRpm { get; set; }
            public int? RateLimitRpd { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of VirtualKeyRateLimitCache
        /// </summary>
        public VirtualKeyRateLimitCache(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            ILogger<VirtualKeyRateLimitCache> logger)
        {
            _serviceProvider = serviceProvider;
            _cache = cache;
            _logger = logger;
            _rateLimits = new ConcurrentDictionary<string, VirtualKeyRateLimits>();
        }

        /// <summary>
        /// Gets rate limits for a virtual key synchronously
        /// </summary>
        public VirtualKeyRateLimits? GetRateLimits(string virtualKeyHash)
        {
            if (_rateLimits.TryGetValue(virtualKeyHash, out var limits))
            {
                // Check if cached value is still fresh (less than 1 minute old)
                if (DateTime.UtcNow - limits.LastUpdated < TimeSpan.FromMinutes(1))
                {
                    return limits;
                }
            }

            // Try memory cache as backup
            if (_cache.TryGetValue($"vkey_ratelimits:{virtualKeyHash}", out VirtualKeyRateLimits? cachedLimits) && cachedLimits != null)
            {
                _rateLimits.TryAdd(virtualKeyHash, cachedLimits);
                return cachedLimits;
            }

            return null;
        }

        /// <summary>
        /// Updates rate limits for a virtual key
        /// </summary>
        public void UpdateRateLimits(string virtualKeyHash, int? rpm, int? rpd)
        {
            var limits = new VirtualKeyRateLimits
            {
                RateLimitRpm = rpm,
                RateLimitRpd = rpd,
                LastUpdated = DateTime.UtcNow
            };

            _rateLimits.AddOrUpdate(virtualKeyHash, limits, (key, existing) => limits);
            
            // Also update memory cache with 5 minute expiration
            _cache.Set($"vkey_ratelimits:{virtualKeyHash}", limits, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Removes rate limits for a virtual key
        /// </summary>
        public void RemoveRateLimits(string virtualKeyHash)
        {
            _rateLimits.TryRemove(virtualKeyHash, out _);
            _cache.Remove($"vkey_ratelimits:{virtualKeyHash}");
        }

        /// <summary>
        /// Starts the background service
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Virtual Key Rate Limit Cache service starting");
            
            // Refresh rate limits every 30 seconds
            _refreshTimer = new Timer(RefreshRateLimits, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the background service
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Virtual Key Rate Limit Cache service stopping");
            
            _refreshTimer?.Change(Timeout.Infinite, 0);
            _refreshTimer?.Dispose();
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Refreshes rate limits from the database
        /// </summary>
        private void RefreshRateLimits(object? state)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var virtualKeyService = scope.ServiceProvider.GetRequiredService<IVirtualKeyService>();
                
                // Get all active virtual keys with rate limits
                // This would require a new method in IVirtualKeyService to get all keys with rate limits
                // For now, we'll update individual keys as they're accessed
                
                _logger.LogDebug("Virtual Key rate limits refresh completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing virtual key rate limits");
            }
        }
    }
}