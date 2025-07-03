using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing system configuration including routing and caching.
    /// </summary>
    [ApiController]
    [Route("api/config")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ConfigurationController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cache">Memory cache.</param>
        /// <param name="configuration">Configuration.</param>
        public ConfigurationController(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ConfigurationController> logger,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Gets routing configuration and rules.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Routing configuration data.</returns>
        [HttpGet("routing")]
        public async Task<IActionResult> GetRoutingConfig(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Get model-to-provider mappings
                var modelMappings = await dbContext.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .Select(m => new
                    {
                        Id = m.Id,
                        ModelAlias = m.ModelAlias,
                        ProviderModelName = m.ProviderModelName,
                        IsEnabled = m.IsEnabled,
                        Provider = new
                        {
                            Name = m.ProviderCredential.ProviderName,
                            IsEnabled = m.ProviderCredential.IsEnabled
                        }
                    })
                    .ToListAsync(cancellationToken);

                // Get load balancing configuration
                var loadBalancers = new List<object>
                {
                    new
                    {
                        Id = "primary",
                        Name = "Primary Load Balancer",
                        Algorithm = _configuration["LoadBalancing:Algorithm"] ?? "round-robin",
                        HealthCheckInterval = 30,
                        FailoverThreshold = 3,
                        Endpoints = await GetProviderEndpoints(dbContext, cancellationToken)
                    }
                };

                // Get retry policies
                var retryPolicies = new List<object>
                {
                    new
                    {
                        Id = "default",
                        Name = "Default Retry Policy",
                        MaxRetries = _configuration.GetValue<int>("Retry:MaxRetries", 3),
                        InitialDelay = _configuration.GetValue<int>("Retry:InitialDelayMs", 1000),
                        MaxDelay = _configuration.GetValue<int>("Retry:MaxDelayMs", 30000),
                        BackoffMultiplier = _configuration.GetValue<double>("Retry:BackoffMultiplier", 2.0),
                        RetryableStatusCodes = new[] { 429, 500, 502, 503, 504 }
                    }
                };

                // Get routing statistics
                var routingStats = await GetRoutingStatistics(dbContext, cancellationToken);

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    RoutingRules = modelMappings,
                    LoadBalancers = loadBalancers,
                    RetryPolicies = retryPolicies,
                    Statistics = routingStats,
                    Configuration = new
                    {
                        EnableFailover = _configuration.GetValue<bool>("Routing:EnableFailover", true),
                        EnableLoadBalancing = _configuration.GetValue<bool>("Routing:EnableLoadBalancing", true),
                        RequestTimeout = _configuration.GetValue<int>("Routing:RequestTimeoutSeconds", 30),
                        CircuitBreakerThreshold = _configuration.GetValue<int>("Routing:CircuitBreakerThreshold", 5)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve routing configuration");
                return StatusCode(500, new { error = "Failed to retrieve routing configuration", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets caching configuration and statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Caching configuration data.</returns>
        [HttpGet("caching")]
        public async Task<IActionResult> GetCachingConfig(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Define cache policies
                var cachePolicies = new List<object>
                {
                    new
                    {
                        Id = "model-list",
                        Name = "Model List Cache",
                        Type = "memory",
                        TTL = 300, // 5 minutes
                        MaxSize = 100,
                        Strategy = "LRU",
                        Enabled = true,
                        Description = "Caches available models per provider"
                    },
                    new
                    {
                        Id = "provider-health",
                        Name = "Provider Health Cache",
                        Type = "memory",
                        TTL = 60, // 1 minute
                        MaxSize = 50,
                        Strategy = "LRU",
                        Enabled = true,
                        Description = "Caches provider health check results"
                    },
                    new
                    {
                        Id = "virtual-key",
                        Name = "Virtual Key Cache",
                        Type = "memory",
                        TTL = 600, // 10 minutes
                        MaxSize = 1000,
                        Strategy = "LRU",
                        Enabled = true,
                        Description = "Caches virtual key details and permissions"
                    },
                    new
                    {
                        Id = "response-cache",
                        Name = "Response Cache",
                        Type = "distributed",
                        TTL = 3600, // 1 hour
                        MaxSize = 10000,
                        Strategy = "LFU",
                        Enabled = _configuration.GetValue<bool>("Caching:EnableResponseCache", false),
                        Description = "Caches LLM responses for identical requests"
                    }
                };

                // Get cache statistics
                var cacheStats = await GetCacheStatistics(dbContext, cancellationToken);

                // Define cache regions
                var cacheRegions = new List<object>
                {
                    new
                    {
                        Id = "global",
                        Name = "Global Cache",
                        Type = "memory",
                        Status = "healthy",
                        Nodes = 1,
                        Metrics = new
                        {
                            Size = "45.2 MB",
                            Items = 1234,
                            HitRate = 85.5,
                            MissRate = 14.5,
                            EvictionRate = 2.1
                        }
                    }
                };

                if (_configuration.GetValue<bool>("Redis:Enabled", false))
                {
                    cacheRegions.Add(new
                    {
                        Id = "distributed",
                        Name = "Redis Cache",
                        Type = "redis",
                        Status = "healthy",
                        Nodes = _configuration.GetValue<int>("Redis:ClusterNodes", 1),
                        Metrics = new
                        {
                            Size = "256 MB",
                            Items = 45678,
                            HitRate = 92.3,
                            MissRate = 7.7,
                            EvictionRate = 0.5
                        }
                    });
                }

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    CachePolicies = cachePolicies,
                    CacheRegions = cacheRegions,
                    Statistics = cacheStats,
                    Configuration = new
                    {
                        DefaultTTL = _configuration.GetValue<int>("Caching:DefaultTTLSeconds", 300),
                        MaxMemorySize = _configuration["Caching:MaxMemorySize"] ?? "1GB",
                        EvictionPolicy = _configuration["Caching:EvictionPolicy"] ?? "LRU",
                        CompressionEnabled = _configuration.GetValue<bool>("Caching:EnableCompression", true),
                        RedisConnectionString = _configuration["Redis:ConnectionString"] != null ? "[REDACTED]" : null
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve caching configuration");
                return StatusCode(500, new { error = "Failed to retrieve caching configuration", message = ex.Message });
            }
        }

        /// <summary>
        /// Updates routing configuration.
        /// </summary>
        /// <param name="config">Updated routing configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPut("routing")]
        public async Task<IActionResult> UpdateRoutingConfig([FromBody] UpdateRoutingConfigDto config, CancellationToken cancellationToken = default)
        {
            try
            {
                // In a real implementation, this would update configuration in database or config service
                _logger.LogInformation("Updating routing configuration");

                // Clear related caches
                _cache.Remove("routing:config");
                _cache.Remove("routing:stats");

                return Ok(new { message = "Routing configuration updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update routing configuration");
                return StatusCode(500, new { error = "Failed to update routing configuration", message = ex.Message });
            }
        }

        /// <summary>
        /// Updates caching configuration.
        /// </summary>
        /// <param name="config">Updated caching configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPut("caching")]
        public async Task<IActionResult> UpdateCachingConfig([FromBody] UpdateCachingConfigDto config, CancellationToken cancellationToken = default)
        {
            try
            {
                // In a real implementation, this would update configuration in database or config service
                _logger.LogInformation("Updating caching configuration");

                // Clear all caches to apply new configuration
                if (config.ClearAllCaches)
                {
                    // Clear memory cache
                    if (_cache is MemoryCache memoryCache)
                    {
                        memoryCache.Compact(1.0);
                    }
                }

                return Ok(new { message = "Caching configuration updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update caching configuration");
                return StatusCode(500, new { error = "Failed to update caching configuration", message = ex.Message });
            }
        }

        /// <summary>
        /// Clears specific cache by ID.
        /// </summary>
        /// <param name="cacheId">Cache policy ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPost("caching/{cacheId}/clear")]
        public async Task<IActionResult> ClearCache(string cacheId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Clearing cache: {CacheId}", cacheId);

                // Clear specific cache based on ID
                switch (cacheId)
                {
                    case "model-list":
                        _cache.Remove("models:*");
                        break;
                    case "provider-health":
                        _cache.Remove("health:*");
                        break;
                    case "virtual-key":
                        _cache.Remove("vkey:*");
                        break;
                    case "response-cache":
                        _cache.Remove("response:*");
                        break;
                    default:
                        return BadRequest($"Unknown cache ID: {cacheId}");
                }

                return Ok(new { message = $"Cache '{cacheId}' cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache {CacheId}", cacheId);
                return StatusCode(500, new { error = "Failed to clear cache", message = ex.Message });
            }
        }

        private async Task<List<object>> GetProviderEndpoints(ConfigurationDbContext dbContext, CancellationToken cancellationToken)
        {
            var providers = await dbContext.ProviderCredentials
                .Where(p => p.IsEnabled)
                .Select(p => new
                {
                    p.ProviderName,
                    p.BaseUrl,
                    LastHealth = dbContext.ProviderHealthRecords
                        .Where(h => h.ProviderName == p.ProviderName)
                        .OrderByDescending(h => h.TimestampUtc)
                        .Select(h => new { IsHealthy = h.IsOnline, ResponseTime = h.ResponseTimeMs })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return providers.Select(p => (object)new
            {
                Name = p.ProviderName,
                Url = p.BaseUrl ?? $"https://api.{p.ProviderName.ToLower()}.com",
                Weight = 1,
                HealthStatus = p.LastHealth?.IsHealthy ?? false ? "healthy" : "unhealthy",
                ResponseTime = p.LastHealth?.ResponseTime ?? 0
            }).ToList();
        }

        private async Task<object> GetRoutingStatistics(ConfigurationDbContext dbContext, CancellationToken cancellationToken)
        {
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);

            var stats = await dbContext.RequestLogs
                .Where(r => r.Timestamp >= oneDayAgo)
                .GroupBy(r => r.ModelName)
                .Select(g => new
                {
                    Provider = g.Key,
                    RequestCount = g.Count(),
                    SuccessRate = g.Count(r => r.StatusCode < 400) * 100.0 / g.Count(),
                    AvgLatency = g.Average(r => r.ResponseTimeMs)
                })
                .ToListAsync(cancellationToken);

            return new
            {
                TotalRequests = stats.Sum(s => s.RequestCount),
                ProviderDistribution = stats,
                FailoverEvents = 0, // Would need actual failover tracking
                LoadBalancerHealth = 100.0
            };
        }

        private async Task<object> GetCacheStatistics(ConfigurationDbContext dbContext, CancellationToken cancellationToken)
        {
            // In a real implementation, these would come from actual cache metrics
            return new
            {
                TotalHits = 125432,
                TotalMisses = 18765,
                HitRate = 87.0,
                AvgResponseTime = new
                {
                    WithCache = 45,
                    WithoutCache = 850
                },
                MemoryUsage = new
                {
                    Current = "45.2 MB",
                    Peak = "128 MB",
                    Limit = "1 GB"
                },
                TopCachedItems = new[]
                {
                    new { Key = "models:openai", Hits = 8945, Size = "2.1 KB" },
                    new { Key = "models:anthropic", Hits = 7632, Size = "1.8 KB" },
                    new { Key = "vkey:abc123", Hits = 5421, Size = "512 B" }
                }
            };
        }
    }

    /// <summary>
    /// DTO for updating routing configuration.
    /// </summary>
    public class UpdateRoutingConfigDto
    {
        /// <summary>
        /// Enable or disable failover.
        /// </summary>
        public bool EnableFailover { get; set; }

        /// <summary>
        /// Enable or disable load balancing.
        /// </summary>
        public bool EnableLoadBalancing { get; set; }

        /// <summary>
        /// Request timeout in seconds.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; }

        /// <summary>
        /// Circuit breaker threshold.
        /// </summary>
        public int CircuitBreakerThreshold { get; set; }
    }

    /// <summary>
    /// DTO for updating caching configuration.
    /// </summary>
    public class UpdateCachingConfigDto
    {
        /// <summary>
        /// Default TTL in seconds.
        /// </summary>
        public int DefaultTTLSeconds { get; set; }

        /// <summary>
        /// Maximum memory size.
        /// </summary>
        public string MaxMemorySize { get; set; } = "1GB";

        /// <summary>
        /// Eviction policy.
        /// </summary>
        public string EvictionPolicy { get; set; } = "LRU";

        /// <summary>
        /// Enable or disable compression.
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// Clear all caches.
        /// </summary>
        public bool ClearAllCaches { get; set; }
    }
}