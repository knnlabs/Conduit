using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
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
    [ServiceFilter(typeof(OperationLoggingFilter))]
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
        public async Task<IActionResult> GetAllSettings()
        {
            var settings = await _globalSettingService.GetAllSettingsAsync();
            return Ok(settings);
        }

        /// <summary>
        /// Gets a global setting by ID
        /// </summary>
        /// <param name="id">The ID of the setting to get</param>
        /// <returns>The global setting</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSettingById(int id)
        {
            var setting = await _globalSettingService.GetSettingByIdAsync(id);
            if (setting == null)
            {
                return this.NotFoundEntity("Global setting", id);
            }
            return Ok(setting);
        }

        /// <summary>
        /// Gets a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to get</param>
        /// <returns>The global setting</returns>
        [HttpGet("by-key/{key}")]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSettingByKey(string key)
        {
            var setting = await _globalSettingService.GetSettingByKeyAsync(key);
            if (setting == null)
            {
                return this.NotFoundEntity("Global setting", key);
            }
            return Ok(setting);
        }

        /// <summary>
        /// Creates a new global setting
        /// </summary>
        /// <param name="setting">The setting to create</param>
        /// <returns>The created setting</returns>
        [HttpPost]
        [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSetting([FromBody] CreateGlobalSettingDto setting)
        {
            var createdSetting = await _globalSettingService.CreateSettingAsync(setting);
            LogAdminAudit("Created", "GlobalSetting", createdSetting.Id, $"Key: {LoggingSanitizer.S(setting.Key)}");
            AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "create");
            return CreatedAtAction(nameof(GetSettingById), new { id = createdSetting.Id }, createdSetting);
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
        public async Task<IActionResult> UpdateSetting(int id, [FromBody] UpdateGlobalSettingDto setting)
        {
            // Ensure ID in route matches ID in body
            if (id != setting.Id)
            {
                return BadRequest("ID in route must match ID in body");
            }

            // Fetch pre-state for change tracking
            var preState = await _globalSettingService.GetSettingByIdAsync(id);
            if (preState == null)
                throw new KeyNotFoundException();

            if (!await _globalSettingService.UpdateSettingAsync(setting))
                throw new KeyNotFoundException();

            // Build change list from pre-state vs request
            var changes = new List<(string Property, string? OldValue, string? NewValue)>();

            if (setting.Value != null && preState.Value != setting.Value)
                changes.Add(("Value", preState.Value, setting.Value));
            if (setting.Description != null && preState.Description != setting.Description)
                changes.Add(("Description", preState.Description, setting.Description));

            if (changes.Count > 0)
            {
                LogAdminAuditWithChanges("GlobalSetting", id, changes,
                    $"Key: {LoggingSanitizer.S(preState.Key)}");
            }
            else
            {
                LogAdminAudit("Updated", "GlobalSetting", id);
            }
            AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "update");

            return NoContent();
        }

        /// <summary>
        /// Updates or creates a global setting by key
        /// </summary>
        /// <param name="setting">The setting data with key, value, and optional description</param>
        /// <returns>No content if successful</returns>
        [HttpPut("by-key")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSettingByKey([FromBody] UpdateGlobalSettingByKeyDto setting)
        {
            if (!await _globalSettingService.UpdateSettingByKeyAsync(setting))
                throw new InvalidOperationException("Failed to update or create global setting");
            LogAdminAudit("Updated", "GlobalSetting", detail: $"Key: {LoggingSanitizer.S(setting.Key)}");
            AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "update");

            return NoContent();
        }

        /// <summary>
        /// Deletes a global setting
        /// </summary>
        /// <param name="id">The ID of the setting to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSetting(int id)
        {
            if (!await _globalSettingService.DeleteSettingAsync(id))
                throw new KeyNotFoundException();
            LogAdminAudit("Deleted", "GlobalSetting", id);
            AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "delete");

            return NoContent();
        }

        /// <summary>
        /// Deletes a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("by-key/{key}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSettingByKey(string key)
        {
            if (!await _globalSettingService.DeleteSettingByKeyAsync(key))
                throw new KeyNotFoundException();
            LogAdminAudit("Deleted", "GlobalSetting", detail: $"Key: {LoggingSanitizer.S(key)}");
            AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "delete");

            return NoContent();
        }

        /// <summary>
        /// Gets global settings cache statistics
        /// </summary>
        /// <returns>Cache statistics including hit rate, size, and invalidation count</returns>
        [HttpGet("cache/stats")]
        [ProducesResponseType(typeof(GlobalSettingCacheStatsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCacheStats()
        {
            var stats = await _cacheService.GetCacheStatsAsync();
            var statsDto = new GlobalSettingCacheStatsDto
            {
                CacheSize = (int)stats["CacheSize"],
                CacheHits = (long)stats["CacheHits"],
                CacheMisses = (long)stats["CacheMisses"],
                Invalidations = (long)stats["Invalidations"],
                HitRate = (double)stats["HitRate"],
                LastLoadTime = (DateTime)stats["LastLoadTime"],
                CachedKeys = (List<string>)stats["CachedKeys"]
            };
            return Ok(statsDto);
        }

        /// <summary>
        /// Reloads all global settings from the database into the cache
        /// </summary>
        /// <returns>No content if successful</returns>
        [HttpPost("cache/reload")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ReloadCache()
        {
            await _cacheService.ReloadAllSettingsAsync();
            LogAdminAudit("Reloaded", "GlobalSettingsCache");

            return NoContent();
        }

        /// <summary>
        /// Invalidates a specific cached setting, forcing it to be reloaded from database on next access
        /// </summary>
        /// <param name="key">The key of the setting to invalidate</param>
        /// <returns>No content if successful</returns>
        [HttpPost("cache/invalidate/{key}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> InvalidateCacheSetting(string key)
        {
            await _cacheService.InvalidateSettingAsync(key);
            LogAdminAudit("Invalidated", "GlobalSettingsCache", detail: $"Key: {LoggingSanitizer.S(key)}");

            return NoContent();
        }
    }
}
