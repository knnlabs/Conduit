namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Service for caching and retrieving global settings from the database.
/// Settings are loaded at application startup and cached in-memory for fast access.
/// Cache is automatically invalidated when settings are modified via the Admin API.
/// </summary>
public interface IGlobalSettingsCacheService
{
    /// <summary>
    /// Gets the maximum number of agentic iterations allowed per request.
    /// </summary>
    /// <returns>
    /// The configured maximum iterations value from GlobalSettings,
    /// or the default value of 5 if the setting doesn't exist.
    /// Value is clamped to the valid range of 1-100.
    /// </returns>
    Task<int> GetMaxAgenticIterationsAsync();

    /// <summary>
    /// Gets the minimum number of agentic iterations allowed per request.
    /// </summary>
    /// <returns>
    /// The configured minimum iterations value from GlobalSettings,
    /// or the default value of 1 if the setting doesn't exist.
    /// Value is clamped to the valid range of 1-100.
    /// </returns>
    Task<int> GetMinAgenticIterationsAsync();

    /// <summary>
    /// Gets the default state for agentic mode when not specified in request.
    /// </summary>
    /// <returns>
    /// The configured default enabled state from GlobalSettings,
    /// or the default value of true if the setting doesn't exist.
    /// </returns>
    Task<bool> GetDefaultAgenticModeEnabledAsync();

    /// <summary>
    /// Gets whether LLM response caching is enabled.
    /// This setting can be toggled at runtime via the Admin API.
    /// </summary>
    /// <returns>
    /// The configured LLM caching enabled state from GlobalSettings,
    /// or the default value of false (disabled) if the setting doesn't exist.
    /// </returns>
    Task<bool> GetLLMCachingEnabledAsync();

    /// <summary>
    /// Gets a raw setting value by key from the cache.
    /// Returns null if the key does not exist.
    /// </summary>
    /// <param name="key">The setting key to retrieve.</param>
    /// <returns>The setting value, or null if not found.</returns>
    Task<string?> GetSettingValueAsync(string key);

    /// <summary>
    /// Invalidates a specific cached setting, forcing it to be reloaded from the database on next access.
    /// </summary>
    /// <param name="settingKey">The key of the setting to invalidate.</param>
    Task InvalidateSettingAsync(string settingKey);

    /// <summary>
    /// Reloads all cached settings from the database.
    /// </summary>
    Task ReloadAllSettingsAsync();

    /// <summary>
    /// Gets cache statistics for monitoring and debugging.
    /// </summary>
    /// <returns>Dictionary containing cache hit/miss counts and other metrics.</returns>
    Task<Dictionary<string, object>> GetCacheStatsAsync();
}
