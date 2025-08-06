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
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs.Cache;
using ConduitLLM.Configuration.DTOs.Routing;

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
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<ConfigurationController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ICacheManagementService _cacheManagementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cache">Memory cache.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="cacheManagementService">Service for cache maintenance operations.</param>
        public ConfigurationController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ConfigurationController> logger,
            IMemoryCache cache,
            IConfiguration configuration,
            ICacheManagementService cacheManagementService)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cacheManagementService = cacheManagementService ?? throw new ArgumentNullException(nameof(cacheManagementService));
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
                    .Include(m => m.Provider)
                    .Select(m => new
                    {
                        Id = m.Id,
                        ModelAlias = m.ModelAlias,
                        ProviderModelId = m.ProviderModelId,
                        IsEnabled = m.IsEnabled,
                        Provider = new
                        {
                            Id = m.Provider.Id,
                            Name = m.Provider.ProviderName,
                            Type = m.Provider.ProviderType,
                            IsEnabled = m.Provider.IsEnabled
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
                var configuration = await _cacheManagementService.GetConfigurationAsync(cancellationToken);
                return Ok(configuration);
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

                // Added to ensure the method remains asynchronous and to avoid CS1998 warning
                await Task.CompletedTask;

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
        public async Task<IActionResult> UpdateCachingConfig([FromBody] UpdateCacheConfigDto config, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cacheManagementService.UpdateConfigurationAsync(config, cancellationToken);
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
                await _cacheManagementService.ClearCacheAsync(cacheId, cancellationToken);
                return Ok(new { message = $"Cache '{cacheId}' cleared successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache {CacheId}", cacheId);
                return StatusCode(500, new { error = "Failed to clear cache", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets cache statistics for all regions or a specific region.
        /// </summary>
        /// <param name="regionId">Optional region ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cache statistics.</returns>
        [HttpGet("caching/statistics")]
        public async Task<IActionResult> GetCacheStatistics([FromQuery] string? regionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var statistics = await _cacheManagementService.GetStatisticsAsync(regionId, cancellationToken);
                return Ok(statistics);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache statistics");
                return StatusCode(500, new { error = "Failed to get cache statistics", message = ex.Message });
            }
        }

        /// <summary>
        /// Lists all cache regions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of cache regions.</returns>
        [HttpGet("caching/regions")]
        public async Task<IActionResult> GetCacheRegions(CancellationToken cancellationToken = default)
        {
            try
            {
                var configuration = await _cacheManagementService.GetConfigurationAsync(cancellationToken);
                return Ok(new
                {
                    Regions = configuration.CacheRegions,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache regions");
                return StatusCode(500, new { error = "Failed to get cache regions", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets entries from a specific cache region.
        /// </summary>
        /// <param name="regionId">Region ID.</param>
        /// <param name="skip">Number of entries to skip.</param>
        /// <param name="take">Number of entries to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cache entries.</returns>
        [HttpGet("caching/{regionId}/entries")]
        public async Task<IActionResult> GetCacheEntries(string regionId, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                if (take > 1000)
                {
                    return BadRequest(new { error = "Cannot retrieve more than 1000 entries at once" });
                }

                var entries = await _cacheManagementService.GetEntriesAsync(regionId, skip, take, cancellationToken);
                return Ok(entries);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache entries for region {RegionId}", regionId);
                return StatusCode(500, new { error = "Failed to get cache entries", message = ex.Message });
            }
        }

        /// <summary>
        /// Forces a refresh of cache entries in a region.
        /// </summary>
        /// <param name="regionId">Region ID.</param>
        /// <param name="key">Optional specific key to refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPost("caching/{regionId}/refresh")]
        public async Task<IActionResult> RefreshCache(string regionId, [FromQuery] string? key = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cacheManagementService.RefreshCacheAsync(regionId, key, cancellationToken);
                var message = string.IsNullOrEmpty(key) 
                    ? $"Cache region '{regionId}' refreshed successfully" 
                    : $"Cache key '{key}' in region '{regionId}' refreshed successfully";
                return Ok(new { message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh cache for region {RegionId}", regionId);
                return StatusCode(500, new { error = "Failed to refresh cache", message = ex.Message });
            }
        }

        /// <summary>
        /// Updates the policy for a specific cache region.
        /// </summary>
        /// <param name="regionId">Region ID.</param>
        /// <param name="policyUpdate">Policy update details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPut("caching/{regionId}/policy")]
        public async Task<IActionResult> UpdateCachePolicy(string regionId, [FromBody] UpdateCachePolicyDto policyUpdate, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cacheManagementService.UpdatePolicyAsync(regionId, policyUpdate, cancellationToken);
                return Ok(new { message = $"Cache policy for region '{regionId}' updated successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update cache policy for region {RegionId}", regionId);
                return StatusCode(500, new { error = "Failed to update cache policy", message = ex.Message });
            }
        }

        private async Task<List<object>> GetProviderEndpoints(ConduitDbContext dbContext, CancellationToken cancellationToken)
        {
            var providers = await dbContext.Providers
                .Where(p => p.IsEnabled)
                .Select(p => new
                {
                    p.Id,
                    p.ProviderName,
                    p.ProviderType,
                    p.BaseUrl
                })
                .ToListAsync(cancellationToken);

            return providers.Select(p => (object)new
            {
                Id = p.Id,
                Name = p.ProviderName,
                Type = p.ProviderType.ToString(),
                Url = p.BaseUrl ?? $"https://api.{p.ProviderType.ToString().ToLower()}.com",
                Weight = 1,
                HealthStatus = "healthy", // Provider health tracking removed
                ResponseTime = 0 // Provider health tracking removed
            }).ToList();
        }

        private async Task<object> GetRoutingStatistics(ConduitDbContext dbContext, CancellationToken cancellationToken)
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

        private async Task<object> GetCacheStatistics(ConduitDbContext dbContext, CancellationToken cancellationToken)
        {
            // In a real implementation, these would come from actual cache metrics
        // Added to ensure the method remains asynchronous and to avoid CS1998 warning
        await Task.CompletedTask;
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

}