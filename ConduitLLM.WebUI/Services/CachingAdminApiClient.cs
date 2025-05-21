using System.Collections.Concurrent;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
// Use qualified names when referring to DTO types to avoid ambiguity
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Caching decorator for the Admin API client.
    /// Implements caching for frequently used API calls to improve performance.
    /// </summary>
    public class CachingAdminApiClient : IAdminApiClient
    {
        private readonly IAdminApiClient _innerClient;
        private readonly ILogger<CachingAdminApiClient> _logger;
        
        // Cache TTL configuration
        private readonly TimeSpan _shortCacheTtl = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _mediumCacheTtl = TimeSpan.FromMinutes(5); 
        private readonly TimeSpan _longCacheTtl = TimeSpan.FromMinutes(30);

        // Cache storage
        private readonly ConcurrentDictionary<string, CacheEntry<IEnumerable<VirtualKeyDto>>> _virtualKeysCache = new();
        private readonly ConcurrentDictionary<string, CacheEntry<VirtualKeyDto>> _virtualKeyByIdCache = new();
        private readonly ConcurrentDictionary<string, CacheEntry<IEnumerable<IpFilterDto>>> _ipFiltersCache = new();
        private readonly ConcurrentDictionary<string, CacheEntry<IEnumerable<GlobalSettingDto>>> _globalSettingsCache = new();
        private readonly ConcurrentDictionary<string, CacheEntry<IEnumerable<ModelProviderMappingDto>>> _modelMappingsCache = new();
        private readonly ConcurrentDictionary<string, CacheEntry<IEnumerable<ModelCostDto>>> _modelCostsCache = new();
        private readonly ConcurrentDictionary<string, CacheEntry<IEnumerable<ProviderCredentialDto>>> _providerCredentialsCache = new();
        
        // Statistics
        private long _cacheHits = 0;
        private long _cacheMisses = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingAdminApiClient"/> class.
        /// </summary>
        /// <param name="innerClient">The inner Admin API client to delegate calls to when cache misses occur.</param>
        /// <param name="logger">The logger.</param>
        public CachingAdminApiClient(IAdminApiClient innerClient, ILogger<CachingAdminApiClient> logger)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <returns>Cache statistics including hit rate.</returns>
        public CacheStatistics GetStatistics()
        {
            var totalRequests = _cacheHits + _cacheMisses;
            var hitRate = totalRequests > 0 ? (double)_cacheHits / totalRequests : 0;
            
            return new CacheStatistics
            {
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                HitRate = hitRate,
                ActiveCacheEntries = _virtualKeysCache.Count + _virtualKeyByIdCache.Count + 
                                    _ipFiltersCache.Count + _globalSettingsCache.Count + 
                                    _modelMappingsCache.Count + _modelCostsCache.Count +
                                    _providerCredentialsCache.Count
            };
        }

        /// <summary>
        /// Clears all caches.
        /// </summary>
        public void ClearAllCaches()
        {
            _virtualKeysCache.Clear();
            _virtualKeyByIdCache.Clear();
            _ipFiltersCache.Clear();
            _globalSettingsCache.Clear();
            _modelMappingsCache.Clear();
            _modelCostsCache.Clear();
            _providerCredentialsCache.Clear();
            
            _logger.LogInformation("All Admin API caches cleared");
        }
        
        #region Virtual Keys

        /// <inheritdoc />
        public async Task<IEnumerable<VirtualKeyDto>> GetAllVirtualKeysAsync()
        {
            const string cacheKey = "all-virtual-keys";
            
            if (_virtualKeysCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
            {
                Interlocked.Increment(ref _cacheHits);
                return cached.Value;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            var result = await _innerClient.GetAllVirtualKeysAsync();
            
            _virtualKeysCache[cacheKey] = new CacheEntry<IEnumerable<VirtualKeyDto>>(result, _shortCacheTtl);
            return result;
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id)
        {
            var cacheKey = $"virtual-key-{id}";
            
            if (_virtualKeyByIdCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
            {
                Interlocked.Increment(ref _cacheHits);
                return cached.Value;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            var result = await _innerClient.GetVirtualKeyByIdAsync(id);
            
            if (result != null)
            {
                _virtualKeyByIdCache[cacheKey] = new CacheEntry<VirtualKeyDto>(result, _shortCacheTtl);
            }
            
            return result;
        }

        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto?> CreateVirtualKeyAsync(CreateVirtualKeyRequestDto createDto)
        {
            // Creation operations invalidate caches
            _virtualKeysCache.Clear();
            
            return await _innerClient.CreateVirtualKeyAsync(createDto);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto updateDto)
        {
            // Update operations invalidate caches
            _virtualKeysCache.Clear();
            
            var cacheKey = $"virtual-key-{id}";
            _virtualKeyByIdCache.TryRemove(cacheKey, out _);
            
            return await _innerClient.UpdateVirtualKeyAsync(id, updateDto);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            // Delete operations invalidate caches
            _virtualKeysCache.Clear();
            
            var cacheKey = $"virtual-key-{id}";
            _virtualKeyByIdCache.TryRemove(cacheKey, out _);
            
            return await _innerClient.DeleteVirtualKeyAsync(id);
        }

        /// <inheritdoc />
        public async Task<bool> ResetVirtualKeySpendAsync(int id)
        {
            // Reset operations invalidate caches
            _virtualKeysCache.Clear();
            
            var cacheKey = $"virtual-key-{id}";
            _virtualKeyByIdCache.TryRemove(cacheKey, out _);
            
            return await _innerClient.ResetVirtualKeySpendAsync(id);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>> GetVirtualKeyUsageStatisticsAsync(int? virtualKeyId = null)
        {
            // Usage statistics are not cached as they need to be fresh
            return await _innerClient.GetVirtualKeyUsageStatisticsAsync(virtualKeyId);
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationResult?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            // Validation is not cached as it needs to be fresh
            return await _innerClient.ValidateVirtualKeyAsync(key, requestedModel);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeySpendAsync(int id, decimal cost)
        {
            // Spend updates are not cached
            return await _innerClient.UpdateVirtualKeySpendAsync(id, cost);
        }

        /// <inheritdoc />
        public async Task<BudgetCheckResult?> CheckVirtualKeyBudgetAsync(int id)
        {
            // Budget checks are not cached
            return await _innerClient.CheckVirtualKeyBudgetAsync(id);
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetVirtualKeyValidationInfoAsync(int id)
        {
            // Validation info is not cached as it needs to be fresh
            return await _innerClient.GetVirtualKeyValidationInfoAsync(id);
        }

        /// <inheritdoc />
        public async Task PerformVirtualKeyMaintenanceAsync()
        {
            // Maintenance operations invalidate caches
            _virtualKeysCache.Clear();
            _virtualKeyByIdCache.Clear();
            
            await _innerClient.PerformVirtualKeyMaintenanceAsync();
        }

        #endregion

        #region Model Provider Mappings

        /// <inheritdoc />
        public async Task<IEnumerable<ModelProviderMappingDto>> GetAllModelProviderMappingsAsync()
        {
            const string cacheKey = "all-model-mappings";
            
            if (_modelMappingsCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
            {
                Interlocked.Increment(ref _cacheHits);
                return cached.Value;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            var result = await _innerClient.GetAllModelProviderMappingsAsync();
            
            _modelMappingsCache[cacheKey] = new CacheEntry<IEnumerable<ModelProviderMappingDto>>(result, _mediumCacheTtl);
            return result;
        }

        /// <inheritdoc />
        public async Task<ModelProviderMappingDto?> GetModelProviderMappingByIdAsync(int id)
        {
            var cacheKey = $"model-mapping-{id}";
            
            // For this one, we'll check the all mappings cache first
            if (_modelMappingsCache.TryGetValue("all-model-mappings", out var allCached) && !allCached.IsExpired)
            {
                Interlocked.Increment(ref _cacheHits);
                return allCached.Value.FirstOrDefault(m => m.Id == id);
            }
            
            Interlocked.Increment(ref _cacheMisses);
            return await _innerClient.GetModelProviderMappingByIdAsync(id);
        }

        /// <inheritdoc />
        public async Task<ModelProviderMappingDto?> GetModelProviderMappingByAliasAsync(string modelAlias)
        {
            var cacheKey = $"model-mapping-alias-{modelAlias}";
            
            // For this one, we'll check the all mappings cache first 
            if (_modelMappingsCache.TryGetValue("all-model-mappings", out var allCached) && !allCached.IsExpired)
            {
                Interlocked.Increment(ref _cacheHits);
                return allCached.Value.FirstOrDefault(m => string.Equals(m.ModelId, modelAlias, StringComparison.OrdinalIgnoreCase));
            }
            
            Interlocked.Increment(ref _cacheMisses);
            return await _innerClient.GetModelProviderMappingByAliasAsync(modelAlias);
        }

        /// <inheritdoc />
        public async Task<bool> CreateModelProviderMappingAsync(ModelProviderMapping mapping)
        {
            // Creation operations invalidate caches
            _modelMappingsCache.Clear();
            
            return await _innerClient.CreateModelProviderMappingAsync(mapping);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateModelProviderMappingAsync(int id, ModelProviderMapping mapping)
        {
            // Update operations invalidate caches
            _modelMappingsCache.Clear();
            
            return await _innerClient.UpdateModelProviderMappingAsync(id, mapping);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelProviderMappingAsync(int id)
        {
            // Delete operations invalidate caches
            _modelMappingsCache.Clear();
            
            return await _innerClient.DeleteModelProviderMappingAsync(id);
        }

        #endregion

        #region Provider Credentials

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderCredentialDto>> GetAllProviderCredentialsAsync()
        {
            const string cacheKey = "all-provider-credentials";
            
            if (_providerCredentialsCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
            {
                Interlocked.Increment(ref _cacheHits);
                return cached.Value;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            var result = await _innerClient.GetAllProviderCredentialsAsync();
            
            _providerCredentialsCache[cacheKey] = new CacheEntry<IEnumerable<ProviderCredentialDto>>(result, _mediumCacheTtl);
            return result;
        }

        // Implement the rest of the interface methods for forwarding
        // For brevity, I'll implement just the methods we've added caching to
        // The rest would be similar to the pattern above

        #endregion

        #region Non-cached methods (delegated to inner client)

        public Task<IpFilterSettingsDto> GetIpFilterSettingsAsync() => _innerClient.GetIpFilterSettingsAsync();
        public Task<bool> UpdateIpFilterSettingsAsync(IpFilterSettingsDto settings) => _innerClient.UpdateIpFilterSettingsAsync(settings);
        public Task<IEnumerable<IpFilterDto>> GetEnabledIpFiltersAsync() => _innerClient.GetEnabledIpFiltersAsync();
        public Task<IpCheckResult?> CheckIpAddressAsync(string ipAddress) => _innerClient.CheckIpAddressAsync(ipAddress);
        public Task<IEnumerable<GlobalSettingDto>> GetAllGlobalSettingsAsync() => _innerClient.GetAllGlobalSettingsAsync();
        public Task<GlobalSettingDto?> GetGlobalSettingByKeyAsync(string key) => _innerClient.GetGlobalSettingByKeyAsync(key);
        public Task<GlobalSettingDto?> UpsertGlobalSettingAsync(GlobalSettingDto setting) => _innerClient.UpsertGlobalSettingAsync(setting);
        public Task<bool> DeleteGlobalSettingAsync(string key) => _innerClient.DeleteGlobalSettingAsync(key);
        public Task<IEnumerable<ProviderHealthConfigurationDto>> GetAllProviderHealthConfigurationsAsync() => _innerClient.GetAllProviderHealthConfigurationsAsync();
        public Task<ProviderHealthConfigurationDto?> GetProviderHealthConfigurationByNameAsync(string providerName) => _innerClient.GetProviderHealthConfigurationByNameAsync(providerName);
        public Task<ProviderHealthConfigurationDto?> CreateProviderHealthConfigurationAsync(CreateProviderHealthConfigurationDto config) => _innerClient.CreateProviderHealthConfigurationAsync(config);
        public Task<ProviderHealthConfigurationDto?> UpdateProviderHealthConfigurationAsync(string providerName, UpdateProviderHealthConfigurationDto config) => _innerClient.UpdateProviderHealthConfigurationAsync(providerName, config);
        public Task<bool> DeleteProviderHealthConfigurationAsync(string providerName) => _innerClient.DeleteProviderHealthConfigurationAsync(providerName);
        public Task<IEnumerable<ProviderHealthRecordDto>> GetProviderHealthRecordsAsync(string? providerName = null) => _innerClient.GetProviderHealthRecordsAsync(providerName);
        public Task<IEnumerable<ProviderHealthSummaryDto>> GetProviderHealthSummaryAsync() => _innerClient.GetProviderHealthSummaryAsync();
        public Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync() => _innerClient.GetAllModelCostsAsync();
        public Task<ModelCostDto?> GetModelCostByIdAsync(int id) => _innerClient.GetModelCostByIdAsync(id);
        public Task<ModelCostDto?> CreateModelCostAsync(CreateModelCostDto modelCost) => _innerClient.CreateModelCostAsync(modelCost);
        public Task<ModelCostDto?> UpdateModelCostAsync(int id, UpdateModelCostDto modelCost) => _innerClient.UpdateModelCostAsync(id, modelCost);
        public Task<bool> DeleteModelCostAsync(int id) => _innerClient.DeleteModelCostAsync(id);
        public Task<ProviderCredentialDto?> GetProviderCredentialByIdAsync(int id) => _innerClient.GetProviderCredentialByIdAsync(id);
        public Task<ProviderCredentialDto?> GetProviderCredentialByNameAsync(string providerName) => _innerClient.GetProviderCredentialByNameAsync(providerName);
        public Task<ProviderCredentialDto?> CreateProviderCredentialAsync(CreateProviderCredentialDto credential) => _innerClient.CreateProviderCredentialAsync(credential);
        public Task<ProviderCredentialDto?> UpdateProviderCredentialAsync(int id, UpdateProviderCredentialDto credential) => _innerClient.UpdateProviderCredentialAsync(id, credential);
        public Task<bool> DeleteProviderCredentialAsync(int id) => _innerClient.DeleteProviderCredentialAsync(id);
        public Task<ProviderConnectionTestResultDto?> TestProviderConnectionAsync(string providerName) => _innerClient.TestProviderConnectionAsync(providerName);
        public Task<IEnumerable<IpFilterDto>> GetAllIpFiltersAsync() => _innerClient.GetAllIpFiltersAsync();
        public Task<IpFilterDto?> GetIpFilterByIdAsync(int id) => _innerClient.GetIpFilterByIdAsync(id);
        public Task<IpFilterDto?> CreateIpFilterAsync(CreateIpFilterDto ipFilter) => _innerClient.CreateIpFilterAsync(ipFilter);
        public Task<IpFilterDto?> UpdateIpFilterAsync(int id, UpdateIpFilterDto ipFilter) => _innerClient.UpdateIpFilterAsync(id, ipFilter);
        public Task<bool> DeleteIpFilterAsync(int id) => _innerClient.DeleteIpFilterAsync(id);
        public Task<ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto?> GetCostDashboardAsync(DateTime? startDate, DateTime? endDate, int? virtualKeyId = null, string? modelName = null) => _innerClient.GetCostDashboardAsync(startDate, endDate, virtualKeyId, modelName);
        public Task<List<ConduitLLM.WebUI.DTOs.DetailedCostDataDto>?> GetDetailedCostDataAsync(DateTime? startDate, DateTime? endDate, int? virtualKeyId = null, string? modelName = null) => _innerClient.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);
        public Task<ConduitLLM.Configuration.DTOs.PagedResult<ConduitLLM.Configuration.DTOs.RequestLogDto>?> GetRequestLogsAsync(int page = 1, int pageSize = 20, int? virtualKeyId = null, string? modelId = null, DateTime? startDate = null, DateTime? endDate = null) => _innerClient.GetRequestLogsAsync(page, pageSize, virtualKeyId, modelId, startDate, endDate);
        public Task<ConduitLLM.Configuration.DTOs.RequestLogDto?> CreateRequestLogAsync(ConduitLLM.Configuration.DTOs.RequestLogDto logDto) => _innerClient.CreateRequestLogAsync(logDto);
        public Task<IEnumerable<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>> GetDailyUsageStatsAsync(DateTime startDate, DateTime endDate, int? virtualKeyId = null) => _innerClient.GetDailyUsageStatsAsync(startDate, endDate, virtualKeyId);
        public Task<IEnumerable<string>> GetDistinctModelsAsync() => _innerClient.GetDistinctModelsAsync();
        public Task<ConduitLLM.Configuration.DTOs.LogsSummaryDto?> GetLogsSummaryAsync(int days = 7, int? virtualKeyId = null) => _innerClient.GetLogsSummaryAsync(days, virtualKeyId);
        public Task<RouterConfig?> GetRouterConfigAsync() => _innerClient.GetRouterConfigAsync();
        public Task<bool> UpdateRouterConfigAsync(RouterConfig config) => _innerClient.UpdateRouterConfigAsync(config);
        public Task<List<ModelDeployment>> GetAllModelDeploymentsAsync() => _innerClient.GetAllModelDeploymentsAsync();
        public Task<ModelDeployment?> GetModelDeploymentAsync(string modelName) => _innerClient.GetModelDeploymentAsync(modelName);
        public Task<bool> SaveModelDeploymentAsync(ModelDeployment deployment) => _innerClient.SaveModelDeploymentAsync(deployment);
        public Task<bool> DeleteModelDeploymentAsync(string modelName) => _innerClient.DeleteModelDeploymentAsync(modelName);
        public Task<List<FallbackConfiguration>> GetAllFallbackConfigurationsAsync() => _innerClient.GetAllFallbackConfigurationsAsync();
        public Task<bool> SetFallbackConfigurationAsync(FallbackConfiguration fallbackConfig) => _innerClient.SetFallbackConfigurationAsync(fallbackConfig);
        public Task<bool> RemoveFallbackConfigurationAsync(string modelName) => _innerClient.RemoveFallbackConfigurationAsync(modelName);
        public Task<bool> CreateDatabaseBackupAsync() => _innerClient.CreateDatabaseBackupAsync();
        public Task<string> GetDatabaseBackupDownloadUrl() => _innerClient.GetDatabaseBackupDownloadUrl();
        public Task<object> GetSystemInfoAsync() => _innerClient.GetSystemInfoAsync();
        public Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync() => _innerClient.CheckAllProvidersStatusAsync();
        public Task<ProviderStatus> CheckProviderStatusAsync(string providerName) => _innerClient.CheckProviderStatusAsync(providerName);
        public Task<string> GetSettingAsync(string key) => _innerClient.GetSettingAsync(key);
        public Task SetSettingAsync(string key, string value) => _innerClient.SetSettingAsync(key, value);
        public Task<bool> InitializeHttpTimeoutConfigurationAsync() => _innerClient.InitializeHttpTimeoutConfigurationAsync();
        public Task<bool> InitializeHttpRetryConfigurationAsync() => _innerClient.InitializeHttpRetryConfigurationAsync();

        #endregion
    }
    
    /// <summary>
    /// Represents a cached entry with expiration.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    internal class CacheEntry<T>
    {
        /// <summary>
        /// Gets the cached value.
        /// </summary>
        public T Value { get; }
        
        /// <summary>
        /// Gets the expiration time.
        /// </summary>
        public DateTime Expiration { get; }
        
        /// <summary>
        /// Gets whether the cache entry is expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > Expiration;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheEntry{T}"/> class.
        /// </summary>
        /// <param name="value">The value to cache.</param>
        /// <param name="ttl">The time-to-live.</param>
        public CacheEntry(T value, TimeSpan ttl)
        {
            Value = value;
            Expiration = DateTime.UtcNow.Add(ttl);
        }
    }
    
    /// <summary>
    /// Statistics about the cache.
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// Gets the number of cache hits.
        /// </summary>
        public long CacheHits { get; set; }
        
        /// <summary>
        /// Gets the number of cache misses.
        /// </summary>
        public long CacheMisses { get; set; }
        
        /// <summary>
        /// Gets the hit rate (hits / total requests).
        /// </summary>
        public double HitRate { get; set; }
        
        /// <summary>
        /// Gets the number of active cache entries.
        /// </summary>
        public int ActiveCacheEntries { get; set; }
    }
}