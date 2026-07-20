using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Services;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cache">Memory cache.</param>
        /// <param name="configuration">Application configuration.</param>
        public ConfigurationController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ConfigurationController> logger,
            IMemoryCache cache,
            IConfiguration configuration)
            : base(logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Gets routing configuration and rules.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Routing configuration data.</returns>
        [HttpGet("routing")]
        [ProducesResponseType(typeof(RoutingConfigurationDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRoutingConfig(CancellationToken cancellationToken = default)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Get model-to-provider mappings
            var modelMappings = await dbContext.ModelProviderMappings
                .Include(m => m.Provider)
                .Select(m => new RoutingRuleDto
                {
                    Id = m.Id,
                    ModelAlias = m.ModelAlias,
                    ProviderModelId = m.ProviderModelId,
                    IsEnabled = m.IsEnabled,
                    Provider = new RoutingRuleProviderDto
                    {
                        Id = m.Provider.Id,
                        Name = m.Provider.ProviderName,
                        Type = m.Provider.ProviderType,
                        IsEnabled = m.Provider.IsEnabled
                    }
                })
                .ToListAsync(cancellationToken);

            // Get load balancing configuration
            var loadBalancers = new List<LoadBalancerDto>
            {
                new LoadBalancerDto
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

            return Ok(new RoutingConfigurationDto
            {
                Timestamp = DateTime.UtcNow,
                RoutingRules = modelMappings,
                LoadBalancers = loadBalancers,
                Statistics = routingStats,
                Configuration = new RoutingSettingsDto
                {
                    EnableFailover = _configuration.GetValue<bool>("Routing:EnableFailover", true),
                    EnableLoadBalancing = _configuration.GetValue<bool>("Routing:EnableLoadBalancing", true),
                    RequestTimeout = _configuration.GetValue<int>("Routing:RequestTimeoutSeconds", 30),
                    CircuitBreakerThreshold = _configuration.GetValue<int>("Routing:CircuitBreakerThreshold", 5)
                }
            });
        }

        private async Task<List<LoadBalancerEndpointDto>> GetProviderEndpoints(ConduitDbContext dbContext, CancellationToken cancellationToken)
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

            return providers.Select(p => new LoadBalancerEndpointDto
            {
                Id = p.Id,
                Name = p.ProviderName,
                Type = p.ProviderType.ToString(),
                Url = p.BaseUrl ?? $"https://api.{p.ProviderType.ToString().ToLower()}.com",
                Weight = 1
            }).ToList();
        }

        private async Task<RoutingStatisticsDto> GetRoutingStatistics(ConduitDbContext dbContext, CancellationToken cancellationToken)
        {
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);

            var stats = await dbContext.RequestLogs
                .Where(r => r.Timestamp >= oneDayAgo)
                .GroupBy(r => r.ModelName)
                .Select(g => new ProviderDistributionDto
                {
                    Provider = g.Key,
                    RequestCount = g.Count(),
                    SuccessRate = g.Count(r => r.StatusCode < 400) * 100.0 / g.Count(),
                    AvgLatency = g.Average(r => r.ResponseTimeMs)
                })
                .ToListAsync(cancellationToken);

            return new RoutingStatisticsDto
            {
                TotalRequests = stats.Sum(s => s.RequestCount),
                ProviderDistribution = stats
            };
        }

    }

}
