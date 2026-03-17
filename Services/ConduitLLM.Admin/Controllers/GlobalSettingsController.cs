using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing global settings
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class GlobalSettingsController : AdminControllerBase
    {
        private readonly IAdminGlobalSettingService _globalSettingService;
        private readonly IGlobalSettingsCacheService _cacheService;

        /// <summary>
        /// Initializes a new instance of the GlobalSettingsController
        /// </summary>
        /// <param name="globalSettingService">The global setting service</param>
        /// <param name="cacheService">The global settings cache service</param>
        /// <param name="logger">The logger</param>
        public GlobalSettingsController(
            IAdminGlobalSettingService globalSettingService,
            IGlobalSettingsCacheService cacheService,
            ILogger<GlobalSettingsController> logger)
            : base(logger)
        {
            _globalSettingService = globalSettingService ?? throw new ArgumentNullException(nameof(globalSettingService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        /// <summary>
        /// Gets all global settings
        /// </summary>
        /// <returns>List of all global settings</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<GlobalSettingDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetAllSettings()
        {
            return ExecuteAsync(
                () => _globalSettingService.GetAllSettingsAsync(),
                Ok,
                "GetAllSettings");
        }

        /// <summary>
        /// Gets a global setting by ID
        /// </summary>
        /// <param name="id">The ID of the setting to get</param>
        /// <returns>The global setting</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetSettingById(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _globalSettingService.GetSettingByIdAsync(id),
                Ok,
                "Global setting",
                id,
                "GetSettingById");
        }

        /// <summary>
        /// Gets a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to get</param>
        /// <returns>The global setting</returns>
        [HttpGet("by-key/{key}")]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetSettingByKey(string key)
        {
            return ExecuteWithNotFoundAsync(
                () => _globalSettingService.GetSettingByKeyAsync(key),
                Ok,
                "Global setting",
                key,
                "GetSettingByKey");
        }

        /// <summary>
        /// Creates a new global setting
        /// </summary>
        /// <param name="setting">The setting to create</param>
        /// <returns>The created setting</returns>
        [HttpPost]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateSetting([FromBody] CreateGlobalSettingDto setting)
        {
            return ExecuteAsync(
                () => _globalSettingService.CreateSettingAsync(setting),
                createdSetting =>
                {
                    LogAdminAudit("Created", "GlobalSetting", createdSetting.Id, $"Key: {LoggingSanitizer.S(setting.Key)}");
                    return CreatedAtAction(nameof(GetSettingById), new { id = createdSetting.Id }, createdSetting);
                },
                "CreateSetting");
        }

        /// <summary>
        /// Updates an existing global setting
        /// </summary>
        /// <param name="id">The ID of the setting to update</param>
        /// <param name="setting">The updated setting data</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> UpdateSetting(int id, [FromBody] UpdateGlobalSettingDto setting)
        {
            // Ensure ID in route matches ID in body
            if (id != setting.Id)
            {
                return Task.FromResult<IActionResult>(BadRequest("ID in route must match ID in body"));
            }

            return ExecuteAsync(
                async () =>
                {
                    if (!await _globalSettingService.UpdateSettingAsync(setting))
                        throw new KeyNotFoundException();
                    LogAdminAudit("Updated", "GlobalSetting", id);
                },
                NoContent(),
                "UpdateSetting",
                new { Id = id });
        }

        /// <summary>
        /// Updates or creates a global setting by key
        /// </summary>
        /// <param name="setting">The setting data with key, value, and optional description</param>
        /// <returns>No content if successful</returns>
        [HttpPut("by-key")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> UpdateSettingByKey([FromBody] UpdateGlobalSettingByKeyDto setting)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (!await _globalSettingService.UpdateSettingByKeyAsync(setting))
                        throw new InvalidOperationException("Failed to update or create global setting");
                    LogAdminAudit("Updated", "GlobalSetting", detail: $"Key: {LoggingSanitizer.S(setting.Key)}");
                },
                NoContent(),
                "UpdateSettingByKey",
                new { Key = setting.Key });
        }

        /// <summary>
        /// Deletes a global setting
        /// </summary>
        /// <param name="id">The ID of the setting to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> DeleteSetting(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (!await _globalSettingService.DeleteSettingAsync(id))
                        throw new KeyNotFoundException();
                    LogAdminAudit("Deleted", "GlobalSetting", id);
                },
                NoContent(),
                "DeleteSetting",
                new { Id = id });
        }

        /// <summary>
        /// Deletes a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("by-key/{key}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> DeleteSettingByKey(string key)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (!await _globalSettingService.DeleteSettingByKeyAsync(key))
                        throw new KeyNotFoundException();
                    LogAdminAudit("Deleted", "GlobalSetting", detail: $"Key: {LoggingSanitizer.S(key)}");
                },
                NoContent(),
                "DeleteSettingByKey",
                new { Key = key });
        }

        /// <summary>
        /// Gets global settings cache statistics
        /// </summary>
        /// <returns>Cache statistics including hit rate, size, and invalidation count</returns>
        [HttpGet("cache/stats")]
        [ProducesResponseType(typeof(GlobalSettingCacheStatsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetCacheStats()
        {
            return ExecuteAsync(
                async () =>
                {
                    var stats = await _cacheService.GetCacheStatsAsync();
                    return new GlobalSettingCacheStatsDto
                    {
                        CacheSize = (int)stats["CacheSize"],
                        CacheHits = (long)stats["CacheHits"],
                        CacheMisses = (long)stats["CacheMisses"],
                        Invalidations = (long)stats["Invalidations"],
                        HitRate = (double)stats["HitRate"],
                        LastLoadTime = (DateTime)stats["LastLoadTime"],
                        CachedKeys = (List<string>)stats["CachedKeys"]
                    };
                },
                Ok,
                "GetCacheStats");
        }

        /// <summary>
        /// Reloads all global settings from the database into the cache
        /// </summary>
        /// <returns>No content if successful</returns>
        [HttpPost("cache/reload")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> ReloadCache()
        {
            return ExecuteAsync(
                async () =>
                {
                    await _cacheService.ReloadAllSettingsAsync();
                    LogAdminAudit("Reloaded", "GlobalSettingsCache");
                },
                NoContent(),
                "ReloadCache");
        }

        /// <summary>
        /// Invalidates a specific cached setting, forcing it to be reloaded from database on next access
        /// </summary>
        /// <param name="key">The key of the setting to invalidate</param>
        /// <returns>No content if successful</returns>
        [HttpPost("cache/invalidate/{key}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> InvalidateCacheSetting(string key)
        {
            return ExecuteAsync(
                async () =>
                {
                    await _cacheService.InvalidateSettingAsync(key);
                    LogAdminAudit("Invalidated", "GlobalSettingsCache", detail: $"Key: {LoggingSanitizer.S(key)}");
                },
                NoContent(),
                "InvalidateCacheSetting",
                new { Key = key });
        }
    }
}
