using System.Text.Json;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.PromptCaching;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models;

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
public class PromptCachingController : AdminControllerBase
{
    private const string SettingKey = "PromptCaching.Config";

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
    public Task<IActionResult> GetConfig()
    {
        return ExecuteAsync(
            async () =>
            {
                var json = await _cacheService.GetSettingValueAsync(SettingKey);
                if (json == null)
                {
                    return new PromptCachingConfigDto
                    {
                        AutoInjectEnabled = false,
                        InjectionPoints = new List<CacheInjectionPointDto>()
                    };
                }

                var config = JsonSerializer.Deserialize<PromptCachingConfig>(json, JsonOptions);
                if (config == null)
                {
                    return new PromptCachingConfigDto
                    {
                        AutoInjectEnabled = false,
                        InjectionPoints = new List<CacheInjectionPointDto>()
                    };
                }

                return new PromptCachingConfigDto
                {
                    AutoInjectEnabled = config.AutoInjectEnabled,
                    InjectionPoints = config.InjectionPoints.Select(p => new CacheInjectionPointDto
                    {
                        Role = p.Role,
                        Index = p.Index
                    }).ToList()
                };
            },
            Ok,
            "GetPromptCachingConfig");
    }

    /// <summary>
    /// Updates the prompt caching configuration.
    /// </summary>
    /// <param name="dto">The new prompt caching configuration.</param>
    /// <returns>The updated configuration.</returns>
    [HttpPut("config")]
    [ProducesResponseType(typeof(PromptCachingConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> UpdateConfig([FromBody] UpdatePromptCachingConfigDto dto)
    {
        return ExecuteAsync(
            async () =>
            {
                // Map DTO to domain model
                var config = new PromptCachingConfig
                {
                    AutoInjectEnabled = dto.AutoInjectEnabled,
                    InjectionPoints = dto.InjectionPoints.Select(p => new CacheInjectionPoint
                    {
                        Role = p.Role,
                        Index = p.Index
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(config, JsonOptions);

                // Upsert: try update first, create if not found
                var existing = await _globalSettingService.GetSettingByKeyAsync(SettingKey);
                if (existing != null)
                {
                    await _globalSettingService.UpdateSettingByKeyAsync(new UpdateGlobalSettingByKeyDto
                    {
                        Key = SettingKey,
                        Value = json,
                        Description = "Prompt caching auto-injection configuration"
                    });
                }
                else
                {
                    await _globalSettingService.CreateSettingAsync(new CreateGlobalSettingDto
                    {
                        Key = SettingKey,
                        Value = json,
                        Description = "Prompt caching auto-injection configuration"
                    });
                }

                // Invalidate cache so changes take effect immediately
                await _cacheService.InvalidateSettingAsync(SettingKey);

                LogAdminAudit("Updated", "PromptCachingConfig", detail: $"AutoInject={dto.AutoInjectEnabled}, Points={dto.InjectionPoints.Count}");

                return new PromptCachingConfigDto
                {
                    AutoInjectEnabled = dto.AutoInjectEnabled,
                    InjectionPoints = dto.InjectionPoints
                };
            },
            Ok,
            "UpdatePromptCachingConfig");
    }
}
