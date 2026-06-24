using ConduitLLM.Configuration;
using ConduitLLM.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Admin.Filters;
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
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class ConfigurationController : AdminControllerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILLMCacheManagementService _llmCacheManagementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cache">Memory cache.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="llmCacheManagementService">Service for LLM cache toggle operations.</param>
        public ConfigurationController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ConfigurationController> logger,
            IMemoryCache cache,
            IConfiguration configuration,
            ILLMCacheManagementService llmCacheManagementService)
            : base(logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _llmCacheManagementService = llmCacheManagementService ?? throw new ArgumentNullException(nameof(llmCacheManagementService));
        }

        /// <summary>
        /// Gets routing configuration and rules.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Routing configuration data.</returns>
        [HttpGet("routing")]
        public async Task<IActionResult> GetRoutingConfig(CancellationToken cancellationToken = default)
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
        public async Task<IActionResult> GetLLMCacheStatus(CancellationToken cancellationToken = default)
        {
            var status = await _llmCacheManagementService.GetLLMCacheStatusAsync(cancellationToken);
            return Ok(status);
        }

        /// <summary>
        /// Toggles LLM caching for all instances.
        /// </summary>
        /// <param name="request">Toggle request with enabled state and reason.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated LLM cache control status.</returns>
        [HttpPost("caching/llm-toggle")]
        [ProducesResponseType(typeof(LLMCacheControlDto), 200)]
        public async Task<IActionResult> ToggleLLMCache([FromBody] ToggleLLMCacheRequest request, CancellationToken cancellationToken = default)
        {
            var userName = User?.Identity?.Name ?? "Unknown";
            var result = await _llmCacheManagementService.ToggleLLMCacheAsync(
                request.Enabled,
                userName,
                request.Reason,
                cancellationToken);
            LogAdminAudit("Toggled", "LLMCache", detail: $"Enabled: {request.Enabled}, Reason: {LoggingSanitizer.S(request.Reason)}");
            return Ok(result);
        }

    }

}
