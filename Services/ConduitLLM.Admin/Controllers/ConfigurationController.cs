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
    public class ConfigurationController : AdminControllerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ICacheManagementService? _cacheManagementService;
        private readonly ILLMCacheManagementService _llmCacheManagementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cache">Memory cache.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="cacheManagementService">Service for cache maintenance operations (optional - required only for general cache endpoints).</param>
        /// <param name="llmCacheManagementService">Service for LLM cache toggle operations.</param>
        public ConfigurationController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ConfigurationController> logger,
            IMemoryCache cache,
            IConfiguration configuration,
            ILLMCacheManagementService llmCacheManagementService,
            ICacheManagementService? cacheManagementService = null)
            : base(logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cacheManagementService = cacheManagementService; // Optional - may be null
            _llmCacheManagementService = llmCacheManagementService ?? throw new ArgumentNullException(nameof(llmCacheManagementService));
        }

        /// <summary>
        /// Gets routing configuration and rules.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Routing configuration data.</returns>
        [HttpGet("routing")]
        public Task<IActionResult> GetRoutingConfig(CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                async () =>
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

                    return (object)new
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
                    };
                },
                Ok,
                "GetRoutingConfig");
        }

        /// <summary>
        /// Gets caching configuration and statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Caching configuration data.</returns>
        [HttpGet("caching")]
        public Task<IActionResult> GetCachingConfig(CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            return ExecuteAsync(
                () => _cacheManagementService.GetConfigurationAsync(cancellationToken),
                Ok,
                "GetCachingConfig");
        }


        /// <summary>
        /// Updates caching configuration.
        /// </summary>
        /// <param name="config">Updated caching configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPut("caching")]
        public Task<IActionResult> UpdateCachingConfig([FromBody] UpdateCacheConfigDto config, CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            return ExecuteAsync(
                async () => { await _cacheManagementService.UpdateConfigurationAsync(config, cancellationToken); },
                Ok(new { message = "Caching configuration updated successfully" }),
                "UpdateCachingConfig");
        }

        /// <summary>
        /// Clears specific cache by ID.
        /// </summary>
        /// <param name="cacheId">Cache policy ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPost("caching/{cacheId}/clear")]
        public Task<IActionResult> ClearCache(string cacheId, CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            return ExecuteAsync(
                async () =>
                {
                    await _cacheManagementService.ClearCacheAsync(cacheId, cancellationToken);
                    return new { message = $"Cache '{cacheId}' cleared successfully" };
                },
                Ok,
                "ClearCache",
                new { CacheId = cacheId });
        }

        /// <summary>
        /// Gets cache statistics for all regions or a specific region.
        /// </summary>
        /// <param name="regionId">Optional region ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cache statistics.</returns>
        [HttpGet("caching/statistics")]
        public Task<IActionResult> GetCacheStatistics([FromQuery] string? regionId = null, CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            return ExecuteAsync(
                () => _cacheManagementService.GetStatisticsAsync(regionId, cancellationToken),
                Ok,
                "GetCacheStatistics",
                new { RegionId = regionId });
        }

        /// <summary>
        /// Lists all cache regions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of cache regions.</returns>
        [HttpGet("caching/regions")]
        public Task<IActionResult> GetCacheRegions(CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            return ExecuteAsync(
                async () =>
                {
                    var configuration = await _cacheManagementService.GetConfigurationAsync(cancellationToken);
                    return (object)new
                    {
                        Regions = configuration.CacheRegions,
                        Timestamp = DateTime.UtcNow
                    };
                },
                Ok,
                "GetCacheRegions");
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
        public Task<IActionResult> GetCacheEntries(string regionId, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            if (take > 1000)
            {
                return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto("Cannot retrieve more than 1000 entries at once")));
            }

            return ExecuteAsync(
                () => _cacheManagementService.GetEntriesAsync(regionId, skip, take, cancellationToken),
                Ok,
                "GetCacheEntries",
                new { RegionId = regionId });
        }

        /// <summary>
        /// Forces a refresh of cache entries in a region.
        /// </summary>
        /// <param name="regionId">Region ID.</param>
        /// <param name="key">Optional specific key to refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPost("caching/{regionId}/refresh")]
        public Task<IActionResult> RefreshCache(string regionId, [FromQuery] string? key = null, CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            return ExecuteAsync(
                async () =>
                {
                    await _cacheManagementService.RefreshCacheAsync(regionId, key, cancellationToken);
                    var message = string.IsNullOrEmpty(key)
                        ? $"Cache region '{regionId}' refreshed successfully"
                        : $"Cache key '{key}' in region '{regionId}' refreshed successfully";
                    return new { message };
                },
                Ok,
                "RefreshCache",
                new { RegionId = regionId, Key = key });
        }

        /// <summary>
        /// Updates the policy for a specific cache region.
        /// </summary>
        /// <param name="regionId">Region ID.</param>
        /// <param name="policyUpdate">Policy update details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpPut("caching/{regionId}/policy")]
        public Task<IActionResult> UpdateCachePolicy(string regionId, [FromBody] UpdateCachePolicyDto policyUpdate, CancellationToken cancellationToken = default)
        {
            if (_cacheManagementService == null)
            {
                return Task.FromResult<IActionResult>(StatusCode(501, new { error = "General cache management service not implemented", message = "This endpoint requires cache infrastructure services that are not currently registered." }));
            }

            return ExecuteAsync(
                async () => { await _cacheManagementService.UpdatePolicyAsync(regionId, policyUpdate, cancellationToken); },
                Ok(new { message = $"Cache policy for region '{regionId}' updated successfully" }),
                "UpdateCachePolicy",
                new { RegionId = regionId });
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

        /// <summary>
        /// Gets the current LLM caching status.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>LLM cache control status.</returns>
        [HttpGet("caching/llm-status")]
        [ProducesResponseType(typeof(LLMCacheControlDto), 200)]
        public Task<IActionResult> GetLLMCacheStatus(CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                () => _llmCacheManagementService.GetLLMCacheStatusAsync(cancellationToken),
                Ok,
                "GetLLMCacheStatus");
        }

        /// <summary>
        /// Toggles LLM caching for all instances.
        /// </summary>
        /// <param name="request">Toggle request with enabled state and reason.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated LLM cache control status.</returns>
        [HttpPost("caching/llm-toggle")]
        [ProducesResponseType(typeof(LLMCacheControlDto), 200)]
        public Task<IActionResult> ToggleLLMCache([FromBody] ToggleLLMCacheRequest request, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                async () =>
                {
                    var userName = User?.Identity?.Name ?? "Unknown";
                    return await _llmCacheManagementService.ToggleLLMCacheAsync(
                        request.Enabled,
                        userName,
                        request.Reason,
                        cancellationToken);
                },
                Ok,
                "ToggleLLMCache");
        }

    }

}
