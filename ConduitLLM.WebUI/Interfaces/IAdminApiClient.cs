using System;
using System.Collections.Generic;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Services;
using ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Provides access to Admin API endpoints for managing Conduit LLM configuration.
    /// </summary>
    public interface IAdminApiClient
    {
        #region Model Provider Mappings

        /// <summary>
        /// Gets all model provider mappings
        /// </summary>
        /// <returns>Collection of model provider mappings</returns>
        Task<IEnumerable<ModelProviderMappingDto>> GetAllModelProviderMappingsAsync();

        /// <summary>
        /// Gets a model provider mapping by ID
        /// </summary>
        /// <param name="id">The ID of the mapping to get</param>
        /// <returns>The model provider mapping, or null if not found</returns>
        Task<ModelProviderMappingDto?> GetModelProviderMappingByIdAsync(int id);

        /// <summary>
        /// Gets a model provider mapping by model alias
        /// </summary>
        /// <param name="modelAlias">The model alias to look up</param>
        /// <returns>The model provider mapping, or null if not found</returns>
        Task<ModelProviderMappingDto?> GetModelProviderMappingByAliasAsync(string modelAlias);

        /// <summary>
        /// Creates a new model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to create</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> CreateModelProviderMappingAsync(ConduitLLM.Configuration.Entities.ModelProviderMapping mapping);

        /// <summary>
        /// Updates a model provider mapping
        /// </summary>
        /// <param name="id">The ID of the mapping to update</param>
        /// <param name="mapping">The updated mapping</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateModelProviderMappingAsync(int id, ConduitLLM.Configuration.Entities.ModelProviderMapping mapping);

        /// <summary>
        /// Deletes a model provider mapping
        /// </summary>
        /// <param name="id">The ID of the mapping to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteModelProviderMappingAsync(int id);

        /// <summary>
        /// Discovers available models for a specific provider
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <returns>List of discovered models with their capabilities</returns>
        Task<IEnumerable<DiscoveredModel>> DiscoverProviderModelsAsync(string providerName);

        /// <summary>
        /// Discovers capabilities for a specific model
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <param name="modelId">The model ID</param>
        /// <returns>Model information with capabilities, or null if not found</returns>
        Task<DiscoveredModel?> DiscoverModelCapabilitiesAsync(string providerName, string modelId);

        #endregion
        #region IP Filter Settings

        /// <summary>
        /// Gets the current IP filter settings
        /// </summary>
        /// <returns>IP filter settings DTO</returns>
        Task<IpFilterSettingsDto> GetIpFilterSettingsAsync();

        /// <summary>
        /// Updates the IP filter settings
        /// </summary>
        /// <param name="settings">The settings to update</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateIpFilterSettingsAsync(IpFilterSettingsDto settings);

        /// <summary>
        /// Gets all enabled IP filters
        /// </summary>
        /// <returns>Collection of enabled IP filters</returns>
        Task<IEnumerable<IpFilterDto>> GetEnabledIpFiltersAsync();

        /// <summary>
        /// Checks if an IP address is allowed based on current filter rules
        /// </summary>
        /// <param name="ipAddress">The IP address to check</param>
        /// <returns>Result indicating if the IP is allowed and reason if denied</returns>
        Task<IpCheckResult?> CheckIpAddressAsync(string ipAddress);

        #endregion
        #region Virtual Keys

        /// <summary>
        /// Gets all virtual keys.
        /// </summary>
        /// <returns>A collection of virtual keys.</returns>
        Task<IEnumerable<VirtualKeyDto>> GetAllVirtualKeysAsync();

        /// <summary>
        /// Gets a virtual key by ID.
        /// </summary>
        /// <param name="id">The ID of the virtual key.</param>
        /// <returns>The virtual key, or null if not found.</returns>
        Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id);

        /// <summary>
        /// Creates a new virtual key.
        /// </summary>
        /// <param name="createDto">The DTO containing information for the new virtual key.</param>
        /// <returns>The created virtual key response, including the generated API key.</returns>
        Task<CreateVirtualKeyResponseDto?> CreateVirtualKeyAsync(CreateVirtualKeyRequestDto createDto);

        /// <summary>
        /// Updates a virtual key.
        /// </summary>
        /// <param name="id">The ID of the virtual key to update.</param>
        /// <param name="updateDto">The DTO containing updated information.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto updateDto);

        /// <summary>
        /// Deletes a virtual key.
        /// </summary>
        /// <param name="id">The ID of the virtual key to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteVirtualKeyAsync(int id);

        /// <summary>
        /// Resets the spend amount for a virtual key.
        /// </summary>
        /// <param name="id">The ID of the virtual key to reset.</param>
        /// <returns>True if the reset was successful, false otherwise.</returns>
        Task<bool> ResetVirtualKeySpendAsync(int id);

        /// <summary>
        /// Gets virtual key usage statistics.
        /// </summary>
        /// <param name="virtualKeyId">Optional virtual key ID to filter statistics.</param>
        /// <returns>A collection of usage statistics DTOs.</returns>
        Task<IEnumerable<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>> GetVirtualKeyUsageStatisticsAsync(int? virtualKeyId = null);

        /// <summary>
        /// Validates a virtual key.
        /// </summary>
        /// <param name="key">The virtual key to validate.</param>
        /// <param name="requestedModel">Optional model being requested.</param>
        /// <returns>Validation result with information about the key.</returns>
        Task<VirtualKeyValidationResult?> ValidateVirtualKeyAsync(string key, string? requestedModel = null);

        /// <summary>
        /// Updates the spend amount for a virtual key.
        /// </summary>
        /// <param name="id">The ID of the virtual key.</param>
        /// <param name="cost">The cost to add to the current spend.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        Task<bool> UpdateVirtualKeySpendAsync(int id, decimal cost);

        /// <summary>
        /// Checks if the budget period has expired and resets if needed.
        /// </summary>
        /// <param name="id">The ID of the virtual key.</param>
        /// <returns>Result indicating if a reset was performed.</returns>
        Task<BudgetCheckResult?> CheckVirtualKeyBudgetAsync(int id);

        /// <summary>
        /// Gets detailed information about a virtual key for validation purposes.
        /// </summary>
        /// <param name="id">The ID of the virtual key.</param>
        /// <returns>Virtual key validation information or null if not found.</returns>
        Task<VirtualKeyValidationInfoDto?> GetVirtualKeyValidationInfoAsync(int id);

        /// <summary>
        /// Performs maintenance tasks on all virtual keys including:
        /// - Resetting expired budgets
        /// - Disabling expired keys
        /// - Checking keys approaching budget limits
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task PerformVirtualKeyMaintenanceAsync();

        #endregion

        #region Global Settings

        /// <summary>
        /// Gets all global settings.
        /// </summary>
        /// <returns>A collection of global settings.</returns>
        Task<IEnumerable<GlobalSettingDto>> GetAllGlobalSettingsAsync();

        /// <summary>
        /// Gets a global setting by key.
        /// </summary>
        /// <param name="key">The key of the global setting.</param>
        /// <returns>The global setting, or null if not found.</returns>
        Task<GlobalSettingDto?> GetGlobalSettingByKeyAsync(string key);

        /// <summary>
        /// Creates or updates a global setting.
        /// </summary>
        /// <param name="setting">The global setting to create or update.</param>
        /// <returns>The created or updated global setting.</returns>
        Task<GlobalSettingDto?> UpsertGlobalSettingAsync(GlobalSettingDto setting);

        /// <summary>
        /// Deletes a global setting.
        /// </summary>
        /// <param name="key">The key of the global setting to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteGlobalSettingAsync(string key);

        #endregion

        #region Provider Health

        /// <summary>
        /// Gets all provider health configurations.
        /// </summary>
        /// <returns>A collection of provider health configurations.</returns>
        Task<IEnumerable<ProviderHealthConfigurationDto>> GetAllProviderHealthConfigurationsAsync();

        /// <summary>
        /// Gets a provider health configuration by provider name.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider health configuration, or null if not found.</returns>
        Task<ProviderHealthConfigurationDto?> GetProviderHealthConfigurationByNameAsync(string providerName);

        /// <summary>
        /// Creates a new provider health configuration.
        /// </summary>
        /// <param name="config">The configuration to create.</param>
        /// <returns>The created configuration.</returns>
        Task<ProviderHealthConfigurationDto?> CreateProviderHealthConfigurationAsync(CreateProviderHealthConfigurationDto config);

        /// <summary>
        /// Updates a provider health configuration.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <param name="config">The updated configuration.</param>
        /// <returns>The updated configuration, or null if the update failed.</returns>
        Task<ProviderHealthConfigurationDto?> UpdateProviderHealthConfigurationAsync(string providerName, UpdateProviderHealthConfigurationDto config);

        /// <summary>
        /// Deletes a provider health configuration.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteProviderHealthConfigurationAsync(string providerName);

        /// <summary>
        /// Gets all provider health records.
        /// </summary>
        /// <param name="providerName">Optional provider name to filter records.</param>
        /// <returns>A collection of provider health records.</returns>
        Task<IEnumerable<ProviderHealthRecordDto>> GetProviderHealthRecordsAsync(string? providerName = null);

        /// <summary>
        /// Gets provider health summary.
        /// </summary>
        /// <returns>A collection of provider health summaries.</returns>
        Task<IEnumerable<ProviderHealthSummaryDto>> GetProviderHealthSummaryAsync();

        /// <summary>
        /// Purges old provider health records.
        /// </summary>
        /// <param name="olderThan">Date threshold - records older than this will be purged</param>
        /// <returns>Number of records purged</returns>
        Task<int> PurgeOldProviderHealthRecordsAsync(DateTime olderThan);

        #endregion

        #region Model Costs

        /// <summary>
        /// Gets all model costs.
        /// </summary>
        /// <returns>A collection of model costs.</returns>
        Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync();

        /// <summary>
        /// Gets a model cost by ID.
        /// </summary>
        /// <param name="id">The ID of the model cost.</param>
        /// <returns>The model cost, or null if not found.</returns>
        Task<ModelCostDto?> GetModelCostByIdAsync(int id);

        /// <summary>
        /// Creates a new model cost.
        /// </summary>
        /// <param name="modelCost">The model cost to create.</param>
        /// <returns>The created model cost.</returns>
        Task<ModelCostDto?> CreateModelCostAsync(CreateModelCostDto modelCost);

        /// <summary>
        /// Updates a model cost.
        /// </summary>
        /// <param name="id">The ID of the model cost to update.</param>
        /// <param name="modelCost">The updated model cost.</param>
        /// <returns>The updated model cost, or null if the update failed.</returns>
        Task<ModelCostDto?> UpdateModelCostAsync(int id, UpdateModelCostDto modelCost);

        /// <summary>
        /// Deletes a model cost.
        /// </summary>
        /// <param name="id">The ID of the model cost to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteModelCostAsync(int id);

        #endregion

        #region Provider Credentials

        /// <summary>
        /// Gets all provider credentials.
        /// </summary>
        /// <returns>A collection of provider credentials.</returns>
        Task<IEnumerable<ProviderCredentialDto>> GetAllProviderCredentialsAsync();

        /// <summary>
        /// Gets a provider credential by ID.
        /// </summary>
        /// <param name="id">The ID of the provider credential.</param>
        /// <returns>The provider credential, or null if not found.</returns>
        Task<ProviderCredentialDto?> GetProviderCredentialByIdAsync(int id);

        /// <summary>
        /// Gets a provider credential by provider name.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider credential, or null if not found.</returns>
        Task<ProviderCredentialDto?> GetProviderCredentialByNameAsync(string providerName);

        /// <summary>
        /// Creates a new provider credential.
        /// </summary>
        /// <param name="credential">The provider credential to create.</param>
        /// <returns>The created provider credential.</returns>
        Task<ProviderCredentialDto?> CreateProviderCredentialAsync(CreateProviderCredentialDto credential);

        /// <summary>
        /// Updates a provider credential.
        /// </summary>
        /// <param name="id">The ID of the provider credential to update.</param>
        /// <param name="credential">The updated provider credential.</param>
        /// <returns>The updated provider credential, or null if the update failed.</returns>
        Task<ProviderCredentialDto?> UpdateProviderCredentialAsync(int id, UpdateProviderCredentialDto credential);

        /// <summary>
        /// Deletes a provider credential.
        /// </summary>
        /// <param name="id">The ID of the provider credential to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteProviderCredentialAsync(int id);

        /// <summary>
        /// Tests a provider connection.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>A result indicating whether the connection was successful.</returns>
        Task<ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto?> TestProviderConnectionAsync(string providerName);

        #endregion

        #region IP Filters

        /// <summary>
        /// Gets all IP filters.
        /// </summary>
        /// <returns>A collection of IP filters.</returns>
        Task<IEnumerable<IpFilterDto>> GetAllIpFiltersAsync();

        /// <summary>
        /// Gets an IP filter by ID.
        /// </summary>
        /// <param name="id">The ID of the IP filter.</param>
        /// <returns>The IP filter, or null if not found.</returns>
        Task<IpFilterDto?> GetIpFilterByIdAsync(int id);

        /// <summary>
        /// Creates a new IP filter.
        /// </summary>
        /// <param name="ipFilter">The IP filter to create.</param>
        /// <returns>The created IP filter.</returns>
        Task<IpFilterDto?> CreateIpFilterAsync(CreateIpFilterDto ipFilter);

        /// <summary>
        /// Updates an IP filter.
        /// </summary>
        /// <param name="id">The ID of the IP filter to update.</param>
        /// <param name="ipFilter">The updated IP filter.</param>
        /// <returns>The updated IP filter, or null if the update failed.</returns>
        Task<IpFilterDto?> UpdateIpFilterAsync(int id, UpdateIpFilterDto ipFilter);

        /// <summary>
        /// Deletes an IP filter.
        /// </summary>
        /// <param name="id">The ID of the IP filter to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteIpFilterAsync(int id);

        #endregion

        #region Cost Dashboard

        /// <summary>
        /// Gets dashboard data for the specified period
        /// </summary>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by</param>
        /// <param name="modelName">Optional model name to filter by</param>
        /// <returns>Cost dashboard data</returns>
        Task<ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto?> GetCostDashboardAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null);

        /// <summary>
        /// Gets detailed cost data for export
        /// </summary>
        /// <param name="startDate">Start date of the period</param>
        /// <param name="endDate">End date of the period</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by</param>
        /// <param name="modelName">Optional model name to filter by</param>
        /// <returns>Detailed cost data</returns>
        Task<List<ConduitLLM.WebUI.DTOs.DetailedCostDataDto>?> GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null);

        #endregion

        #region Logs

        /// <summary>
        /// Gets request logs with optional filtering and pagination.
        /// </summary>
        /// <param name="page">The page number (1-based).</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter.</param>
        /// <param name="modelId">Optional model ID filter.</param>
        /// <param name="startDate">Optional start date filter.</param>
        /// <param name="endDate">Optional end date filter.</param>
        /// <returns>A paged result of request logs.</returns>
        Task<ConduitLLM.Configuration.DTOs.PagedResult<ConduitLLM.Configuration.DTOs.RequestLogDto>?> GetRequestLogsAsync(
            int page = 1,
            int pageSize = 20,
            int? virtualKeyId = null,
            string? modelId = null,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Creates a new request log.
        /// </summary>
        /// <param name="logDto">The request log data.</param>
        /// <returns>The created request log.</returns>
        Task<ConduitLLM.Configuration.DTOs.RequestLogDto?> CreateRequestLogAsync(ConduitLLM.Configuration.DTOs.RequestLogDto logDto);

        /// <summary>
        /// Gets daily usage statistics.
        /// </summary>
        /// <param name="startDate">Start date for the statistics.</param>
        /// <param name="endDate">End date for the statistics.</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter.</param>
        /// <returns>A collection of daily usage statistics.</returns>
        Task<IEnumerable<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>> GetDailyUsageStatsAsync(
            DateTime startDate,
            DateTime endDate,
            int? virtualKeyId = null);

        /// <summary>
        /// Gets distinct model names used in logs.
        /// </summary>
        /// <returns>A collection of distinct model names.</returns>
        Task<IEnumerable<string>> GetDistinctModelsAsync();

        /// <summary>
        /// Gets log summary statistics.
        /// </summary>
        /// <param name="days">Number of days to include in the summary.</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter.</param>
        /// <returns>Summary statistics.</returns>
        Task<ConduitLLM.Configuration.DTOs.LogsSummaryDto?> GetLogsSummaryAsync(int days = 7, int? virtualKeyId = null);

        #endregion

        #region Router

        /// <summary>
        /// Gets the router configuration
        /// </summary>
        /// <returns>The router configuration</returns>
        Task<ConduitLLM.Core.Models.Routing.RouterConfig?> GetRouterConfigAsync();

        /// <summary>
        /// Updates the router configuration
        /// </summary>
        /// <param name="config">The updated router configuration</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateRouterConfigAsync(ConduitLLM.Core.Models.Routing.RouterConfig config);

        /// <summary>
        /// Gets all model deployments
        /// </summary>
        /// <returns>A collection of model deployments</returns>
        Task<List<ConduitLLM.Core.Models.Routing.ModelDeployment>> GetAllModelDeploymentsAsync();

        /// <summary>
        /// Gets a model deployment by name
        /// </summary>
        /// <param name="modelName">The name of the model deployment</param>
        /// <returns>The model deployment, or null if not found</returns>
        Task<ConduitLLM.Core.Models.Routing.ModelDeployment?> GetModelDeploymentAsync(string modelName);

        /// <summary>
        /// Saves a model deployment
        /// </summary>
        /// <param name="deployment">The model deployment to save</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SaveModelDeploymentAsync(ConduitLLM.Core.Models.Routing.ModelDeployment deployment);

        /// <summary>
        /// Deletes a model deployment
        /// </summary>
        /// <param name="modelName">The name of the model deployment to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteModelDeploymentAsync(string modelName);

        /// <summary>
        /// Gets all fallback configurations
        /// </summary>
        /// <returns>A collection of fallback configurations</returns>
        Task<List<ConduitLLM.Core.Models.Routing.FallbackConfiguration>> GetAllFallbackConfigurationsAsync();

        /// <summary>
        /// Sets a fallback configuration
        /// </summary>
        /// <param name="fallbackConfig">The fallback configuration to set</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SetFallbackConfigurationAsync(ConduitLLM.Core.Models.Routing.FallbackConfiguration fallbackConfig);

        /// <summary>
        /// Removes a fallback configuration
        /// </summary>
        /// <param name="modelName">The name of the model to remove the fallback configuration for</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RemoveFallbackConfigurationAsync(string modelName);

        #endregion

        #region Database Backup

        /// <summary>
        /// Creates a database backup
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> CreateDatabaseBackupAsync();

        /// <summary>
        /// Gets the URL for downloading a database backup
        /// </summary>
        /// <returns>The download URL</returns>
        Task<string> GetDatabaseBackupDownloadUrl();

        /// <summary>
        /// Gets system information
        /// </summary>
        /// <returns>System information</returns>
        Task<object> GetSystemInfoAsync();

        #endregion

        #region Provider Status

        /// <summary>
        /// Checks the status of all providers
        /// </summary>
        /// <returns>A dictionary mapping provider names to their status</returns>
        Task<Dictionary<string, ConduitLLM.WebUI.Models.ProviderStatus>> CheckAllProvidersStatusAsync();

        /// <summary>
        /// Checks the status of a specific provider
        /// </summary>
        /// <param name="providerName">The name of the provider to check</param>
        /// <returns>The provider status</returns>
        Task<ConduitLLM.WebUI.Models.ProviderStatus> CheckProviderStatusAsync(string providerName);

        #endregion

        #region Audio Configuration

        /// <summary>
        /// Gets all audio provider configurations.
        /// </summary>
        /// <returns>List of audio provider configurations</returns>
        Task<List<AudioProviderConfigDto>> GetAudioProvidersAsync();

        /// <summary>
        /// Gets a specific audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <returns>The audio provider configuration</returns>
        Task<AudioProviderConfigDto?> GetAudioProviderAsync(int id);

        /// <summary>
        /// Gets audio provider configurations by provider name.
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <returns>List of configurations for the provider</returns>
        Task<List<AudioProviderConfigDto>> GetAudioProvidersByNameAsync(string providerName);

        /// <summary>
        /// Gets enabled providers for a specific audio operation.
        /// </summary>
        /// <param name="operationType">The operation type (transcription, tts, realtime)</param>
        /// <returns>List of enabled providers</returns>
        Task<List<AudioProviderConfigDto>> GetEnabledAudioProvidersAsync(string operationType);

        /// <summary>
        /// Creates a new audio provider configuration.
        /// </summary>
        /// <param name="providerConfig">The provider configuration to create</param>
        /// <returns>The created provider configuration</returns>
        Task<AudioProviderConfigDto> CreateAudioProviderAsync(AudioProviderConfigDto providerConfig);

        /// <summary>
        /// Updates an audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <param name="providerConfig">The updated configuration</param>
        /// <returns>The updated provider configuration</returns>
        Task<AudioProviderConfigDto?> UpdateAudioProviderAsync(int id, AudioProviderConfigDto providerConfig);

        /// <summary>
        /// Deletes an audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteAudioProviderAsync(int id);

        /// <summary>
        /// Tests audio provider connectivity.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <param name="operationType">The operation type to test</param>
        /// <returns>The test results</returns>
        Task<ConduitLLM.WebUI.Services.AudioProviderTestResult> TestAudioProviderAsync(int id, string operationType = "transcription");

        /// <summary>
        /// Gets all audio cost configurations.
        /// </summary>
        /// <returns>List of audio cost configurations</returns>
        Task<List<AudioCostDto>> GetAudioCostsAsync();

        /// <summary>
        /// Gets a specific audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <returns>The audio cost configuration</returns>
        Task<AudioCostDto?> GetAudioCostAsync(int id);

        /// <summary>
        /// Gets audio costs by provider.
        /// </summary>
        /// <param name="provider">The provider name</param>
        /// <returns>List of costs for the provider</returns>
        Task<List<AudioCostDto>> GetAudioCostsByProviderAsync(string provider);

        /// <summary>
        /// Gets the current cost for a specific operation.
        /// </summary>
        /// <param name="provider">The provider name</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="model">The model name (optional)</param>
        /// <returns>The current cost</returns>
        Task<AudioCostDto?> GetCurrentAudioCostAsync(string provider, string operationType, string? model = null);

        /// <summary>
        /// Creates a new audio cost configuration.
        /// </summary>
        /// <param name="cost">The cost configuration to create</param>
        /// <returns>The created cost configuration</returns>
        Task<AudioCostDto> CreateAudioCostAsync(AudioCostDto cost);

        /// <summary>
        /// Updates an audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <param name="cost">The updated configuration</param>
        /// <returns>The updated cost configuration</returns>
        Task<AudioCostDto?> UpdateAudioCostAsync(int id, AudioCostDto cost);

        /// <summary>
        /// Deletes an audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteAudioCostAsync(int id);

        /// <summary>
        /// Gets audio usage logs with pagination and filtering.
        /// </summary>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="virtualKey">Filter by virtual key (optional)</param>
        /// <param name="provider">Filter by provider (optional)</param>
        /// <param name="operationType">Filter by operation type (optional)</param>
        /// <param name="startDate">Start date filter (optional)</param>
        /// <param name="endDate">End date filter (optional)</param>
        /// <returns>Paginated usage logs</returns>
        Task<PagedResult<AudioUsageDto>> GetAudioUsageLogsAsync(
            int pageNumber = 1,
            int pageSize = 50,
            string? virtualKey = null,
            string? provider = null,
            string? operationType = null,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Gets audio usage summary statistics.
        /// </summary>
        /// <param name="startDate">Start date for the summary</param>
        /// <param name="endDate">End date for the summary</param>
        /// <param name="virtualKey">Filter by virtual key (optional)</param>
        /// <param name="provider">Filter by provider (optional)</param>
        /// <returns>Usage summary</returns>
        Task<ConduitLLM.Configuration.DTOs.Audio.AudioUsageSummaryDto> GetAudioUsageSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            string? virtualKey = null,
            string? provider = null);

        /// <summary>
        /// Gets real-time session metrics.
        /// </summary>
        /// <returns>Session metrics</returns>
        Task<ConduitLLM.WebUI.Services.RealtimeSessionMetricsDto> GetRealtimeSessionMetricsAsync();

        /// <summary>
        /// Gets active real-time sessions.
        /// </summary>
        /// <returns>List of active sessions</returns>
        Task<List<RealtimeSessionDto>> GetActiveRealtimeSessionsAsync();

        /// <summary>
        /// Gets details of a specific real-time session.
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>Session details</returns>
        Task<RealtimeSessionDto?> GetRealtimeSessionDetailsAsync(string sessionId);

        /// <summary>
        /// Terminates an active real-time session.
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>True if terminated successfully</returns>
        Task<bool> TerminateRealtimeSessionAsync(string sessionId);

        #endregion

        #region HTTP Configuration

        /// <summary>
        /// Gets a global setting by key
        /// </summary>
        /// <param name="key">The key of the setting to retrieve</param>
        /// <returns>The setting value as a string, or null if not found</returns>
        Task<string?> GetSettingAsync(string key);

        /// <summary>
        /// Sets a global setting value
        /// </summary>
        /// <param name="key">The key of the setting</param>
        /// <param name="value">The value to set</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SetSettingAsync(string key, string value);

        /// <summary>
        /// Initializes HTTP timeout configuration
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        Task<bool> InitializeHttpTimeoutConfigurationAsync();

        /// <summary>
        /// Initializes HTTP retry configuration
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        Task<bool> InitializeHttpRetryConfigurationAsync();

        #endregion
    }
}
