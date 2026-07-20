using System.Text.Json;

using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.PromptCaching;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing prompt caching configuration.
/// Provides a typed API over the PromptCaching.Config global setting.
/// </summary>
[ApiController]
[Route("api/prompt-caching")]
[Authorize(Policy = "MasterKeyPolicy")]
[ServiceFilter(typeof(OperationLoggingFilter))]
public class PromptCachingController : AdminControllerBase
{
    private const string SettingKey = PromptCachingConstants.SettingsKey;

    private readonly IAdminGlobalSettingService _globalSettingService;
    private readonly IGlobalSettingsCacheService _cacheService;
    private readonly IDbContextFactory<ConduitDbContext>? _dbContextFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptCachingController"/> class.
    /// </summary>
    /// <param name="globalSettingService">The global setting service for CRUD operations.</param>
    /// <param name="cacheService">The cache service for reading and invalidating settings.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="dbContextFactory">Optional context factory used for prompt-cache analytics.</param>
    public PromptCachingController(
        IAdminGlobalSettingService globalSettingService,
        IGlobalSettingsCacheService cacheService,
        ILogger<PromptCachingController> logger,
        IDbContextFactory<ConduitDbContext>? dbContextFactory = null)
        : base(logger)
    {
        _globalSettingService = globalSettingService ?? throw new ArgumentNullException(nameof(globalSettingService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Gets the current prompt caching configuration.
    /// </summary>
    /// <returns>The current prompt caching configuration, or defaults if not set.</returns>
    [HttpGet("config")]
    [ProducesResponseType(typeof(PromptCachingConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetConfig()
    {
        var json = await _cacheService.GetSettingValueAsync(SettingKey);
        if (json == null)
        {
            return Ok(new PromptCachingConfigDto
            {
                SchemaVersion = PromptCachingConstants.SchemaVersion,
                Enabled = false
            });
        }

        PromptCachingConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<PromptCachingConfig>(json, JsonOptions);
        }
        catch (JsonException)
        {
            config = null;
        }
        if (config is not null) config = PromptCachingPolicyResolver.Migrate(config);
        var errors = config is null ? Array.Empty<string>() : PromptCachingPolicyResolver.Validate(config);
        if (config == null || errors.Count > 0)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Prompt caching configuration is invalid",
                Detail = config == null ? "The stored configuration is malformed." : string.Join("; ", errors),
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["code"] = "prompt_caching_config_version_unsupported" }
            });
        }

        return Ok(ToDto(config));
    }

    /// <summary>
    /// Updates the prompt caching configuration.
    /// </summary>
    /// <param name="dto">The new prompt caching configuration.</param>
    /// <returns>The updated configuration.</returns>
    [HttpPut("config")]
    [ProducesResponseType(typeof(PromptCachingConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdatePromptCachingConfigDto dto)
    {
        var config = new PromptCachingConfig
        {
            SchemaVersion = dto.SchemaVersion,
            Enabled = dto.Enabled,
            Rules = dto.Rules.Select(r => new PromptCachingRule
            {
                Name = r.Name,
                Enabled = r.Enabled,
                Provider = r.Provider,
                ModelPattern = r.ModelPattern,
                Strategy = Enum.TryParse<PromptCachingStrategy>(r.Strategy, true, out var strategy)
                    ? strategy : (PromptCachingStrategy)(-1),
                Ttl = r.Ttl,
                InjectionPoints = r.InjectionPoints.Select(p => new CacheInjectionPoint { Role = p.Role, Index = p.Index }).ToList()
            }).ToList()
        };

        var validationErrors = PromptCachingPolicyResolver.Validate(config).ToList();
        for (var i = 0; i < dto.Rules.Count; i++)
            if (!Enum.TryParse<PromptCachingStrategy>(dto.Rules[i].Strategy, true, out _))
                validationErrors.Add($"rules[{i}].strategy is not supported.");
        if (validationErrors.Count > 0)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["config"] = validationErrors.ToArray()
            }));

        var json = JsonSerializer.Serialize(config, JsonOptions);

        // Upsert: try update first, create if not found
        var existing = await _globalSettingService.GetSettingByKeyAsync(SettingKey);
        if (existing != null)
        {
            await _globalSettingService.UpdateSettingByKeyAsync(new UpdateGlobalSettingByKeyDto
            {
                Key = SettingKey,
                Value = json,
                Description = "Provider-aware prompt caching policy"
            });
        }
        else
        {
            await _globalSettingService.CreateSettingAsync(new CreateGlobalSettingDto
            {
                Key = SettingKey,
                Value = json,
                Description = "Provider-aware prompt caching policy"
            });
        }

        // Invalidate cache so changes take effect immediately
        await _cacheService.InvalidateSettingAsync(SettingKey);

        LogAdminAudit("Updated", "PromptCachingConfig", detail: $"Enabled={dto.Enabled}, Rules={dto.Rules.Count}");

        return Ok(ToDto(config));
    }

    [HttpGet("capabilities")]
    [ProducesResponseType(typeof(IReadOnlyList<PromptCachingCapabilityDto>), StatusCodes.Status200OK)]
    public IActionResult GetCapabilities() => Ok(PromptCachingProviderAdapters.Capabilities.Select(c => new PromptCachingCapabilityDto
    {
        Provider = c.Provider,
        ModelPattern = c.ModelPattern,
        Strategies = c.Strategies.Select(s => s.ToString()).ToList(),
        Ttls = c.Ttls.ToList(),
        MinimumTokens = c.MinimumTokens,
        MaxBreakpoints = c.MaxBreakpoints,
        ProviderManaged = c.ProviderManaged
    }).ToList());

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(PromptCachingAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? alias = null,
        [FromQuery] string? provider = null,
        [FromQuery] int? mappingId = null,
        CancellationToken cancellationToken = default)
    {
        if (_dbContextFactory is null) return StatusCode(503, "Analytics storage is unavailable.");
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = from?.ToUniversalTime() ?? end.AddHours(-24);
        if (start > end || end - start > TimeSpan.FromDays(90))
            return BadRequest("The analytics range must be ordered and no longer than 90 days.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.RequestLogs.AsNoTracking().Where(log => log.Timestamp >= start && log.Timestamp <= end && log.RequestType == "chat");
        if (!string.IsNullOrWhiteSpace(alias)) query = query.Where(log => log.ModelName == alias);
        if (!string.IsNullOrWhiteSpace(provider)) query = query.Where(log => log.ProviderType == provider);
        if (mappingId.HasValue) query = query.Where(log => log.ModelProviderMappingId == mappingId);
        var rows = await query.Select(log => new
        {
            log.ProviderType, log.ModelProviderMappingId, log.PromptCachingEligible, log.CachedInputTokens,
            log.CachedWriteTokens, log.CachedReadSavings, log.CacheWritePremium, log.ResponseTimeMs,
            log.RoutingAffinityUsed, log.RoutingFailoverCount
        }).ToListAsync(cancellationToken);
        var hits = rows.Where(row => row.CachedInputTokens is > 0).ToList();
        var misses = rows.Where(row => row.PromptCachingEligible && row.CachedInputTokens is not > 0).ToList();
        return Ok(new PromptCachingAnalyticsDto
        {
            From = start, To = end, Requests = rows.Count, EligibleMisses = misses.Count,
            ReadEvents = hits.Count, WriteEvents = rows.Count(row => row.CachedWriteTokens is > 0),
            UnknownOutcomes = rows.Count(row => row.PromptCachingEligible && !row.CachedInputTokens.HasValue && !row.CachedWriteTokens.HasValue),
            CachedTokens = rows.Sum(row => (long)(row.CachedInputTokens ?? 0)),
            GrossSavings = rows.Sum(row => row.CachedReadSavings), WritePremium = rows.Sum(row => row.CacheWritePremium),
            HitLatencyMs = hits.Count == 0 ? null : hits.Average(row => row.ResponseTimeMs),
            MissLatencyMs = misses.Count == 0 ? null : misses.Average(row => row.ResponseTimeMs),
            AffinityReuse = rows.Count(row => row.RoutingAffinityUsed), Failovers = rows.Sum(row => row.RoutingFailoverCount),
            ProviderDistribution = rows.GroupBy(row => new { Provider = row.ProviderType ?? "unknown", row.ModelProviderMappingId })
                .Select(group => new PromptCachingProviderDistributionDto
                { Provider = group.Key.Provider, MappingId = group.Key.ModelProviderMappingId, Requests = group.Count() }).ToList()
        });
    }

    private static PromptCachingConfigDto ToDto(PromptCachingConfig config) => new()
    {
        SchemaVersion = config.SchemaVersion,
        Enabled = config.Enabled,
        Rules = config.Rules.Select(r => new PromptCachingRuleDto
        {
            Name = r.Name,
            Enabled = r.Enabled,
            Provider = r.Provider,
            ModelPattern = r.ModelPattern,
            Strategy = r.Strategy.ToString(),
            Ttl = r.Ttl,
            InjectionPoints = r.InjectionPoints.Select(p => new CacheInjectionPointDto { Role = p.Role, Index = p.Index }).ToList()
        }).ToList()
    };
}
