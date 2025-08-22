using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs.Cache;

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


                // Get routing statistics
                var routingStats = await GetRoutingStatistics(dbContext, cancellationToken);

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    RoutingRules = modelMappings,
                    LoadBalancers = loadBalancers,
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
                    return BadRequest(new ErrorResponseDto("Cannot retrieve more than 1000 entries at once"));
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
                Weight = 1
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
                ProviderDistribution = stats
            };
        }

    }

}
