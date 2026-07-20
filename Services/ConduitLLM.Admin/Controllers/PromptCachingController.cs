using System.Text.Json;

using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.PromptCaching;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public PromptCachingController(
        IAdminGlobalSettingService globalSettingService,
        IGlobalSettingsCacheService cacheService,
        ILogger<PromptCachingController> logger)
        : base(logger)
    {
        _globalSettingService = globalSettingService ?? throw new ArgumentNullException(nameof(globalSettingService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
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
    public IActionResult GetCapabilities() => Ok(PromptCachingCapabilityCatalog.All.Select(c => new PromptCachingCapabilityDto
    {
        Provider = c.Provider,
        ModelPattern = c.ModelPattern,
        Strategies = c.Strategies.Select(s => s.ToString()).ToList(),
        Ttls = c.Ttls.ToList(),
        MinimumTokens = c.MinimumTokens,
        MaxBreakpoints = c.MaxBreakpoints,
        ProviderManaged = c.ProviderManaged
    }).ToList());

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
