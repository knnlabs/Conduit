using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Services;
using System.Text.Json;
using ConduitLLM.Configuration.Entities;

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
                    Priority = m.RoutingPriority,
                    Weight = m.RoutingWeight,
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
                },
                AliasPolicies = await dbContext.ModelRoutePolicies.AsNoTracking().Select(policy => new RoutePolicyDto
                {
                    ModelAlias = policy.ModelAlias, Strategy = policy.Strategy, CostWeight = policy.CostWeight,
                    SpeedWeight = policy.SpeedWeight, QualityWeight = policy.QualityWeight,
                    CacheAffinityEnabled = policy.CacheAffinityEnabled, AffinityTtlSeconds = policy.AffinityTtlSeconds,
                    MaxAffinityScorePenalty = policy.MaxAffinityScorePenalty, IsEnabled = policy.IsEnabled
                }).ToListAsync(cancellationToken)
            });
        }

        [HttpGet("routing/defaults")]
        [ProducesResponseType(typeof(RoutingDefaultsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRoutingDefaults(CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var setting = await context.GlobalSettings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Key == "Routing.Defaults", cancellationToken);
            return Ok(setting is null ? new RoutingDefaultsDto() :
                JsonSerializer.Deserialize<RoutingDefaultsDto>(setting.Value) ?? new RoutingDefaultsDto());
        }

        [HttpPut("routing/defaults")]
        [ProducesResponseType(typeof(RoutingDefaultsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> PutRoutingDefaults([FromBody] RoutingDefaultsDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.CostWeight + dto.SpeedWeight + dto.QualityWeight <= 0)
                return BadRequest("At least one route score weight must be positive.");
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var setting = await context.GlobalSettings.SingleOrDefaultAsync(item => item.Key == "Routing.Defaults", cancellationToken);
            if (setting is null)
            {
                setting = new GlobalSetting { Key = "Routing.Defaults", Description = "Default provider-aware chat routing policy" };
                context.GlobalSettings.Add(setting);
            }
            setting.Value = JsonSerializer.Serialize(dto); setting.UpdatedAt = DateTime.UtcNow;
            var switchSetting = await context.GlobalSettings.SingleOrDefaultAsync(item => item.Key == "Routing.Chat.Enabled", cancellationToken);
            if (switchSetting is null)
            {
                switchSetting = new GlobalSetting { Key = "Routing.Chat.Enabled", Description = "Emergency provider-aware chat routing switch" };
                context.GlobalSettings.Add(switchSetting);
            }
            switchSetting.Value = dto.ChatRoutingEnabled.ToString(); switchSetting.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return Ok(dto);
        }

        [HttpGet("routing/aliases/{alias}")]
        [ProducesResponseType(typeof(RoutePolicyDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAliasRouting(string alias, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var policy = await context.ModelRoutePolicies.AsNoTracking().SingleOrDefaultAsync(item => item.ModelAlias == alias, cancellationToken);
            return policy is null ? NotFound() : Ok(ToRoutePolicyDto(policy));
        }

        [HttpPut("routing/aliases/{alias}")]
        [ProducesResponseType(typeof(RoutePolicyDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> PutAliasRouting(string alias, [FromBody] RoutePolicyDto dto,
            CancellationToken cancellationToken = default)
        {
            if (!dto.Strategy.Equals("Balanced", StringComparison.OrdinalIgnoreCase) ||
                dto.CostWeight + dto.SpeedWeight + dto.QualityWeight <= 0) return BadRequest("A valid Balanced policy is required.");
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var exists = await context.ModelProviderMappings.AnyAsync(mapping => mapping.ModelAlias == alias, cancellationToken);
            if (!exists) return NotFound();
            var policy = await context.ModelRoutePolicies.SingleOrDefaultAsync(item => item.ModelAlias == alias, cancellationToken);
            if (policy is null) { policy = new ModelRoutePolicy { ModelAlias = alias }; context.ModelRoutePolicies.Add(policy); }
            policy.Strategy = "Balanced"; policy.CostWeight = dto.CostWeight; policy.SpeedWeight = dto.SpeedWeight;
            policy.QualityWeight = dto.QualityWeight; policy.CacheAffinityEnabled = dto.CacheAffinityEnabled;
            policy.AffinityTtlSeconds = dto.AffinityTtlSeconds; policy.MaxAffinityScorePenalty = dto.MaxAffinityScorePenalty;
            policy.IsEnabled = dto.IsEnabled; policy.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return Ok(ToRoutePolicyDto(policy));
        }

        private static RoutePolicyDto ToRoutePolicyDto(ModelRoutePolicy policy) => new()
        {
            ModelAlias = policy.ModelAlias, Strategy = policy.Strategy, CostWeight = policy.CostWeight,
            SpeedWeight = policy.SpeedWeight, QualityWeight = policy.QualityWeight,
            CacheAffinityEnabled = policy.CacheAffinityEnabled, AffinityTtlSeconds = policy.AffinityTtlSeconds,
            MaxAffinityScorePenalty = policy.MaxAffinityScorePenalty, IsEnabled = policy.IsEnabled
        };

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
