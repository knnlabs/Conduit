using System.Collections.Concurrent;
using ConduitLLM.Configuration.DTOs.Cache;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// In-memory cache service for GlobalSettings that loads settings at startup
/// and provides fast, strongly-typed access to configuration values.
/// Automatically invalidates cache when settings are modified via Admin API.
/// </summary>
public class GlobalSettingsCacheService : IHostedService, IGlobalSettingsCacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GlobalSettingsCacheService> _logger;
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Setting keys
    private const string KEY_MAX_AGENTIC_ITERATIONS = "Agentic.MaxIterations";
    private const string KEY_MIN_AGENTIC_ITERATIONS = "Agentic.MinIterations";
    private const string KEY_DEFAULT_AGENTIC_ENABLED = "Agentic.DefaultEnabled";
    private const string KEY_LLM_CACHING_ENABLED = "LLM.Caching.Enabled";

    // Default values
    private const int DEFAULT_MAX_AGENTIC_ITERATIONS = 5;
    private const int DEFAULT_MIN_AGENTIC_ITERATIONS = 1;
    private const bool DEFAULT_AGENTIC_ENABLED = true;
    private const bool DEFAULT_LLM_CACHING_ENABLED = false;

    // Validation constants
    private const int MIN_VALID_ITERATIONS = 1;
    private const int MAX_VALID_ITERATIONS = 100;

    // Statistics
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private long _invalidations = 0;
    private DateTime _lastLoadTime = DateTime.MinValue;

    public GlobalSettingsCacheService(
        IServiceScopeFactory scopeFactory,
        ILogger<GlobalSettingsCacheService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts the service and loads all settings into cache.
    /// Called automatically by the hosting infrastructure.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GlobalSettingsCacheService starting - loading settings from database");

        try
        {
            await LoadAllSettingsAsync(cancellationToken);
            _logger.LogInformation("GlobalSettingsCacheService started successfully - {Count} settings loaded", _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load global settings on startup - service will use defaults");
            // Don't throw - allow service to start with defaults
        }
    }

    /// <summary>
    /// Stops the service and clears the cache.
    /// Called automatically by the hosting infrastructure.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GlobalSettingsCacheService stopping");
        _cache.Clear();
        return Task.CompletedTask;
    }

    public async Task<int> GetMaxAgenticIterationsAsync()
    {
        var value = await GetSettingAsync(KEY_MAX_AGENTIC_ITERATIONS);

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogDebug("Max agentic iterations setting not found, using default: {Default}", DEFAULT_MAX_AGENTIC_ITERATIONS);
            return DEFAULT_MAX_AGENTIC_ITERATIONS;
        }

        if (!int.TryParse(value, out var maxIterations))
        {
            _logger.LogWarning("Failed to parse max agentic iterations value '{Value}', using default: {Default}",
                value, DEFAULT_MAX_AGENTIC_ITERATIONS);
            return DEFAULT_MAX_AGENTIC_ITERATIONS;
        }

        // Clamp to valid range
        var clamped = Math.Clamp(maxIterations, MIN_VALID_ITERATIONS, MAX_VALID_ITERATIONS);
        if (clamped != maxIterations)
        {
            _logger.LogWarning("Max agentic iterations {Value} out of valid range ({Min}-{Max}), clamping to {Clamped}",
                maxIterations, MIN_VALID_ITERATIONS, MAX_VALID_ITERATIONS, clamped);
        }

        return clamped;
    }

    public async Task<int> GetMinAgenticIterationsAsync()
    {
        var value = await GetSettingAsync(KEY_MIN_AGENTIC_ITERATIONS);

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogDebug("Min agentic iterations setting not found, using default: {Default}", DEFAULT_MIN_AGENTIC_ITERATIONS);
            return DEFAULT_MIN_AGENTIC_ITERATIONS;
        }

        if (!int.TryParse(value, out var minIterations))
        {
            _logger.LogWarning("Failed to parse min agentic iterations value '{Value}', using default: {Default}",
                value, DEFAULT_MIN_AGENTIC_ITERATIONS);
            return DEFAULT_MIN_AGENTIC_ITERATIONS;
        }

        // Clamp to valid range
        var clamped = Math.Clamp(minIterations, MIN_VALID_ITERATIONS, MAX_VALID_ITERATIONS);
        if (clamped != minIterations)
        {
            _logger.LogWarning("Min agentic iterations {Value} out of valid range ({Min}-{Max}), clamping to {Clamped}",
                minIterations, MIN_VALID_ITERATIONS, MAX_VALID_ITERATIONS, clamped);
        }

        return clamped;
    }

    public async Task<bool> GetDefaultAgenticModeEnabledAsync()
    {
        var value = await GetSettingAsync(KEY_DEFAULT_AGENTIC_ENABLED);

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogDebug("Default agentic enabled setting not found, using default: {Default}", DEFAULT_AGENTIC_ENABLED);
            return DEFAULT_AGENTIC_ENABLED;
        }

        if (bool.TryParse(value, out var enabled))
        {
            return enabled;
        }

        // Try parsing common string representations
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == "1" || normalized == "yes" || normalized == "on")
        {
            return true;
        }
        if (normalized == "0" || normalized == "no" || normalized == "off")
        {
            return false;
        }

        _logger.LogWarning("Failed to parse default agentic enabled value '{Value}', using default: {Default}",
            value, DEFAULT_AGENTIC_ENABLED);
        return DEFAULT_AGENTIC_ENABLED;
    }

    public async Task<bool> GetLLMCachingEnabledAsync()
    {
        var value = await GetSettingAsync(KEY_LLM_CACHING_ENABLED);

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogDebug("LLM caching enabled setting not found, using default: {Default}", DEFAULT_LLM_CACHING_ENABLED);
            return DEFAULT_LLM_CACHING_ENABLED;
        }

        // The value is stored as JSON metadata, try to parse it
        try
        {
            var metadata = System.Text.Json.JsonSerializer.Deserialize<LLMCacheMetadata>(value);
            if (metadata != null)
            {
                return metadata.Enabled;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Fall back to direct boolean parsing if not JSON
        }

        if (bool.TryParse(value, out var enabled))
        {
            return enabled;
        }

        // Try parsing common string representations
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == "1" || normalized == "yes" || normalized == "on")
        {
            return true;
        }
        if (normalized == "0" || normalized == "no" || normalized == "off")
        {
            return false;
        }

        _logger.LogWarning("Failed to parse LLM caching enabled value '{Value}', using default: {Default}",
            value, DEFAULT_LLM_CACHING_ENABLED);
        return DEFAULT_LLM_CACHING_ENABLED;
    }

    public async Task InvalidateSettingAsync(string settingKey)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cache.TryRemove(settingKey, out var oldValue))
            {
                Interlocked.Increment(ref _invalidations);
                _logger.LogInformation("Invalidated cached setting '{Key}' (old value: '{Value}')", settingKey, oldValue);

                // Reload the setting from database immediately
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                    var newSetting = await repository.GetByKeyAsync(settingKey);
                    if (newSetting != null)
                    {
                        _cache.TryAdd(settingKey, newSetting.Value);
                        _logger.LogInformation("Reloaded setting '{Key}' with new value: '{Value}'", settingKey, newSetting.Value);
                    }
                }
            }
            else
            {
                _logger.LogDebug("Attempted to invalidate non-cached setting '{Key}'", settingKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating setting '{Key}'", settingKey);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReloadAllSettingsAsync()
    {
        _logger.LogInformation("Reloading all global settings from database");

        await _lock.WaitAsync();
        try
        {
            _cache.Clear();
            await LoadAllSettingsAsync(CancellationToken.None);
            _logger.LogInformation("Reloaded {Count} settings from database", _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading all settings");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<Dictionary<string, object>> GetCacheStatsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            ["CacheSize"] = _cache.Count,
            ["CacheHits"] = _cacheHits,
            ["CacheMisses"] = _cacheMisses,
            ["Invalidations"] = _invalidations,
            ["HitRate"] = _cacheHits + _cacheMisses > 0
                ? (double)_cacheHits / (_cacheHits + _cacheMisses) * 100
                : 0,
            ["LastLoadTime"] = _lastLoadTime,
            ["CachedKeys"] = _cache.Keys.ToList()
        };

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Gets a setting value from cache, or returns null if not found.
    /// </summary>
    private Task<string?> GetSettingAsync(string key)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            Interlocked.Increment(ref _cacheHits);
            return Task.FromResult<string?>(value);
        }

        Interlocked.Increment(ref _cacheMisses);
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Loads all settings from database into the cache.
    /// </summary>
    private async Task LoadAllSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var settings = await repository.GetAllUnboundedAsync();

                foreach (var setting in settings)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    _cache.TryAdd(setting.Key, setting.Value);
                }

                _lastLoadTime = DateTime.UtcNow;
                _logger.LogDebug("Loaded {Count} settings into cache", _cache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings from database");
            throw;
        }
    }
}
