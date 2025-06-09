using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Models;
// Use qualified names when referring to DTO types to avoid ambiguity
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Client for interacting with the Admin API endpoints.
    /// This class is organized into regions by functional area.
    /// Some functionality is also split into partial classes:
    /// - AdminApiClient.VirtualKeys.cs - Additional virtual key operations
    /// - AdminApiClient.RequestLogs.cs - Request logging operations
    /// - AdminApiClient.ProviderStatus.cs - Provider status operations
    /// - AdminApiClient.ModelProviderMapping.cs - Model mapping operations
    /// - AdminApiClient.IpFilters.cs - IP filter operations
    /// - AdminApiClient.HttpConfig.cs - HTTP configuration operations
    /// - AdminApiClient.CostDashboard.cs - Cost dashboard operations
    /// </summary>
    public partial class AdminApiClient : IAdminApiClient, ConduitLLM.WebUI.Interfaces.IGlobalSettingService, ConduitLLM.WebUI.Interfaces.IVirtualKeyService, IRouterService, ConduitLLM.WebUI.Interfaces.IProviderCredentialService
    {
        // This method from IAdminApiClient is implemented in AdminApiClient.VirtualKeys.cs
        // using the name GetVirtualKeyValidationResultAsync to avoid method name collision
        // with IVirtualKeyService.ValidateVirtualKeyAsync
        public async Task<VirtualKeyValidationResult?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            return await GetVirtualKeyValidationResultAsync(key, requestedModel);
        }

        private readonly HttpClient _httpClient;
        private readonly ILogger<AdminApiClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminApiClient"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="options">The Admin API options.</param>
        /// <param name="logger">The logger.</param>
        public AdminApiClient(
            HttpClient httpClient,
            IOptions<AdminApiOptions> options,
            ILogger<AdminApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var adminOptions = options?.Value ?? new AdminApiOptions();
            
            // Configure base URL
            if (!string.IsNullOrEmpty(adminOptions.BaseUrl))
            {
                _httpClient.BaseAddress = new Uri(adminOptions.BaseUrl);
                _logger.LogInformation("AdminApiClient configured with base URL: {BaseUrl}", adminOptions.BaseUrl);
            }

            // Configure timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(adminOptions.TimeoutSeconds);
            
            // Configure authentication headers
            if (!string.IsNullOrEmpty(adminOptions.MasterKey))
            {
                // Remove any existing headers to avoid duplicates
                if (_httpClient.DefaultRequestHeaders.Contains("X-Master-Key"))
                {
                    _httpClient.DefaultRequestHeaders.Remove("X-Master-Key");
                }
                
                if (_httpClient.DefaultRequestHeaders.Contains("X-API-Key"))
                {
                    _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
                }
                
                // Add the master key header - use X-API-Key as expected by AdminAuthenticationMiddleware
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", adminOptions.MasterKey);
                _logger.LogInformation("AdminApiClient configured with master key (length: {Length})", adminOptions.MasterKey.Length);
            }
            else
            {
                _logger.LogWarning("AdminApiClient initialized without a master key!");
            }
        }

        #region Virtual Keys

        /// <inheritdoc />
        public async Task<IEnumerable<VirtualKeyDto>> GetAllVirtualKeysAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/virtualkeys");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<VirtualKeyDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<VirtualKeyDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual keys from Admin API");
                return Enumerable.Empty<VirtualKeyDto>();
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/virtualkeys/{id}");
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<VirtualKeyDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key with ID {VirtualKeyId} from Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto?> CreateVirtualKeyAsync(CreateVirtualKeyRequestDto createDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/virtualkeys", createDto);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<CreateVirtualKeyResponseDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating virtual key in Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto updateDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/virtualkeys/{id}", updateDto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virtual key with ID {VirtualKeyId} in Admin API", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/virtualkeys/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting virtual key with ID {VirtualKeyId} from Admin API", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResetVirtualKeySpendAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/virtualkeys/{id}/reset-spend", new StringContent(string.Empty));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting virtual key spend with ID {VirtualKeyId} from Admin API", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>> GetVirtualKeyUsageStatisticsAsync(int? virtualKeyId = null)
        {
            try
            {
                string url = "api/costs/virtualkeys";
                if (virtualKeyId.HasValue)
                {
                    url += $"?virtualKeyId={virtualKeyId.Value}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Try to deserialize as Configuration DTOs (Costs namespace)
                var costsDtos = await response.Content.ReadFromJsonAsync<IEnumerable<ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto>>(_jsonOptions);
                
                if (costsDtos != null)
                {
                    // Convert Configuration.DTOs.Costs DTOs to WebUI DTOs
                    return costsDtos.Select(dto => new DTOs.VirtualKeyCostDataDto
                    {
                        VirtualKeyId = dto.VirtualKeyId,
                        KeyName = dto.KeyName,
                        Cost = dto.Cost,
                        RequestCount = dto.RequestCount,
                        // Default values for extended properties
                        InputTokens = 0,
                        OutputTokens = 0,
                        AverageResponseTimeMs = 0,
                        LastUsedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        LastDayRequests = 0
                    }).ToList();
                }
                
                // Fallback: try deserialization as Configuration DTOs (base namespace)
                var baseDtos = await response.Content.ReadFromJsonAsync<IEnumerable<ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto>>(_jsonOptions);
                
                if (baseDtos != null)
                {
                    // Convert Configuration.DTOs DTOs to WebUI DTOs
                    return baseDtos.Select(dto => new ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto
                    {
                        VirtualKeyId = dto.VirtualKeyId,
                        KeyName = dto.KeyName,
                        Cost = dto.Cost,
                        RequestCount = dto.RequestCount
                    }).ToList();
                }
                
                // Return empty if no deserialization worked
                return Enumerable.Empty<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key usage statistics from Admin API");
                return Enumerable.Empty<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>();
            }
        }

        #endregion

        #region Global Settings

        /// <inheritdoc />
        public async Task<IEnumerable<GlobalSettingDto>> GetAllGlobalSettingsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/globalsettings");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<GlobalSettingDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<GlobalSettingDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving global settings from Admin API");
                return Enumerable.Empty<GlobalSettingDto>();
            }
        }

        /// <inheritdoc />
        public async Task<GlobalSettingDto?> GetGlobalSettingByKeyAsync(string key)
        {
            try
            {
                // Use "by-key" endpoint per the GlobalSettingsController route pattern
                var response = await _httpClient.GetAsync($"api/globalsettings/by-key/{Uri.EscapeDataString(key)}");
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<GlobalSettingDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving global setting with key {Key} from Admin API", key);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<GlobalSettingDto?> UpsertGlobalSettingAsync(GlobalSettingDto setting)
        {
            try
            {
                // For upsert, we need to use PUT with by-key endpoint with UpdateGlobalSettingByKeyDto
                var updateDto = new ConduitLLM.Configuration.DTOs.UpdateGlobalSettingByKeyDto
                {
                    Key = setting.Key,
                    Value = setting.Value,
                    Description = setting.Description
                };
                
                var response = await _httpClient.PutAsJsonAsync("api/globalsettings/by-key", updateDto);
                
                if (response.IsSuccessStatusCode)
                {
                    // Return the setting since PUT doesn't return content
                    return setting;
                }
                
                response.EnsureSuccessStatusCode();
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting global setting with key {Key} in Admin API", setting.Key);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteGlobalSettingAsync(string key)
        {
            try
            {
                // Use "by-key" endpoint per the GlobalSettingsController route pattern
                var response = await _httpClient.DeleteAsync($"api/globalsettings/by-key/{Uri.EscapeDataString(key)}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting global setting with key {Key} from Admin API", key);
                return false;
            }
        }

        #endregion

        #region Provider Health

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthConfigurationDto>> GetAllProviderHealthConfigurationsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/providerhealth/configurations");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ProviderHealthConfigurationDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<ProviderHealthConfigurationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health configurations from Admin API");
                return Enumerable.Empty<ProviderHealthConfigurationDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto?> GetProviderHealthConfigurationByNameAsync(string providerName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/providerhealth/configurations/{Uri.EscapeDataString(providerName)}");
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProviderHealthConfigurationDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health configuration for provider {ProviderName} from Admin API", providerName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto?> CreateProviderHealthConfigurationAsync(CreateProviderHealthConfigurationDto config)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/providerhealth/configurations", config);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProviderHealthConfigurationDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider health configuration for provider {ProviderName} in Admin API", config.ProviderName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto?> UpdateProviderHealthConfigurationAsync(string providerName, UpdateProviderHealthConfigurationDto config)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/providerhealth/configurations/{Uri.EscapeDataString(providerName)}", config);
                response.EnsureSuccessStatusCode();
                
                // Admin API returns 204 No Content for successful updates, not a ProviderHealthConfigurationDto
                // So we need to fetch the updated record separately
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    // Fetch the updated provider health configuration to return it
                    return await GetProviderHealthConfigurationByNameAsync(providerName);
                }
                
                // Fallback in case the API changes to return content
                return await response.Content.ReadFromJsonAsync<ProviderHealthConfigurationDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider health configuration for provider {ProviderName} in Admin API", providerName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProviderHealthConfigurationAsync(string providerName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/providerhealth/configurations/{Uri.EscapeDataString(providerName)}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider health configuration for provider {ProviderName} from Admin API", providerName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthRecordDto>> GetProviderHealthRecordsAsync(string? providerName = null)
        {
            try
            {
                string url = "api/providerhealth/records";
                if (!string.IsNullOrEmpty(providerName))
                {
                    url += $"?providerName={Uri.EscapeDataString(providerName)}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ProviderHealthRecordDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<ProviderHealthRecordDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health records from Admin API");
                return Enumerable.Empty<ProviderHealthRecordDto>();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthSummaryDto>> GetProviderHealthSummaryAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/providerhealth/summary");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ProviderHealthSummaryDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<ProviderHealthSummaryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health summary from Admin API");
                return Enumerable.Empty<ProviderHealthSummaryDto>();
            }
        }

        #endregion

        #region Model Costs

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/modelcosts");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ModelCostDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<ModelCostDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model costs from Admin API");
                return Enumerable.Empty<ModelCostDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> GetModelCostByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/modelcosts/{id}");
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ModelCostDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model cost with ID {ModelCostId} from Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> CreateModelCostAsync(CreateModelCostDto modelCost)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/modelcosts", modelCost);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ModelCostDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost in Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> UpdateModelCostAsync(int id, UpdateModelCostDto modelCost)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/modelcosts/{id}", modelCost);
                response.EnsureSuccessStatusCode();
                
                // Admin API returns 204 No Content for successful updates, not a ModelCostDto
                // So we need to fetch the updated record separately
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    // Fetch the updated model cost to return it
                    return await GetModelCostByIdAsync(id);
                }
                
                // Fallback in case the API changes to return content
                return await response.Content.ReadFromJsonAsync<ModelCostDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model cost with ID {ModelCostId} in Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelCostAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/modelcosts/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model cost with ID {ModelCostId} from Admin API", id);
                return false;
            }
        }

        #endregion

        #region Model Provider Mappings

        /// <inheritdoc />
        public async Task<IEnumerable<ModelProviderMappingDto>> GetAllModelProviderMappingsAsync()
        {
            try
            {
                // Use "modelprovider" instead of "modelprovidermappings" to match controller name
                var response = await _httpClient.GetAsync("api/modelprovidermapping");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ModelProviderMappingDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<ModelProviderMappingDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model provider mappings from Admin API");
                return Enumerable.Empty<ModelProviderMappingDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ModelProviderMappingDto?> GetModelProviderMappingByIdAsync(int id)
        {
            try
            {
                // Use "modelprovider" instead of "modelprovidermappings" to match controller name
                var response = await _httpClient.GetAsync($"api/modelprovidermapping/{id}");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ModelProviderMappingDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model provider mapping with ID {MappingId} from Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ModelProviderMappingDto?> GetModelProviderMappingByAliasAsync(string modelAlias)
        {
            try
            {
                // Use "modelprovider" and "by-model" instead to match controller route 
                var response = await _httpClient.GetAsync($"api/modelprovidermapping/by-model/{Uri.EscapeDataString(modelAlias)}");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ModelProviderMappingDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model provider mapping for model alias {ModelAlias} from Admin API", modelAlias);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> CreateModelProviderMappingAsync(ConduitLLM.Configuration.Entities.ModelProviderMapping mapping)
        {
            try
            {
                // Use "modelprovider" instead of "modelprovidermappings" to match controller name
                var response = await _httpClient.PostAsJsonAsync("api/modelprovidermapping", mapping);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping in Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateModelProviderMappingAsync(int id, ConduitLLM.Configuration.Entities.ModelProviderMapping mapping)
        {
            try
            {
                // Use "modelprovider" instead of "modelprovidermappings" to match controller name
                var response = await _httpClient.PutAsJsonAsync($"api/modelprovidermapping/{id}", mapping);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {MappingId} in Admin API", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelProviderMappingAsync(int id)
        {
            try
            {
                // Use "modelprovider" instead of "modelprovidermappings" to match controller name
                var response = await _httpClient.DeleteAsync($"api/modelprovidermapping/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping with ID {MappingId} from Admin API", id);
                return false;
            }
        }

        #endregion

        #region Provider Credentials

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderCredentialDto>> GetAllProviderCredentialsAsync()
        {
            try
            {
                // Use plural "providercredentials" instead of singular to match controller name
                var response = await _httpClient.GetAsync("api/providercredentials");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ProviderCredentialDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<ProviderCredentialDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider credentials from Admin API");
                return Enumerable.Empty<ProviderCredentialDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetProviderCredentialByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/providercredentials/{id}");
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProviderCredentialDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider credential with ID {ProviderId} from Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetProviderCredentialByNameAsync(string providerName)
        {
            try
            {
                // Use "name" instead of "by-name" to match controller route
                var response = await _httpClient.GetAsync($"api/providercredentials/name/{Uri.EscapeDataString(providerName)}");
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProviderCredentialDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider credential for provider {ProviderName} from Admin API", providerName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> CreateProviderCredentialAsync(CreateProviderCredentialDto credential)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/providercredentials", credential);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProviderCredentialDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential in Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> UpdateProviderCredentialAsync(int id, UpdateProviderCredentialDto credential)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/providercredentials/{id}", credential);
                response.EnsureSuccessStatusCode();
                
                // Admin API returns 204 No Content for successful updates, not a ProviderCredentialDto
                // So we need to fetch the updated record separately
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    // Fetch the updated provider credential to return it
                    return await GetProviderCredentialByIdAsync(id);
                }
                
                // Fallback in case the API changes to return content
                return await response.Content.ReadFromJsonAsync<ProviderCredentialDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential with ID {ProviderId} in Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProviderCredentialAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/providercredentials/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential with ID {ProviderId} from Admin API", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto?> TestProviderConnectionAsync(string providerName)
        {
            try
            {
                // Use "test/{id}" route instead of "test-connection/{name}" to match controller
                // We need to get the provider by name first
                var provider = await GetProviderCredentialByNameAsync(providerName);
                if (provider == null)
                {
                    return new ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto
                    {
                        Success = false,
                        Message = $"Provider '{providerName}' not found",
                        ErrorDetails = "Provider not found in the system",
                        ProviderName = providerName
                    };
                }
                
                var response = await _httpClient.PostAsync(
                    $"api/providercredentials/test/{provider.Id}",
                    new StringContent(string.Empty, Encoding.UTF8, "application/json"));
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider {ProviderName} in Admin API", providerName);
                return new ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = "Error testing provider connection",
                    ErrorDetails = ex.Message,
                    ProviderName = providerName
                };
            }
        }

        /// <summary>
        /// Tests a provider connection with given credentials (without saving)
        /// </summary>
        /// <param name="providerCredential">The provider credentials to test</param>
        /// <returns>The test result</returns>
        public async Task<ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto?> TestProviderConnectionWithCredentialsAsync(ProviderCredentialDto providerCredential)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/providercredentials/test",
                    providerCredential,
                    _jsonOptions);
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for provider {ProviderName} in Admin API", providerCredential?.ProviderName);
                return new ConduitLLM.Configuration.DTOs.ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = "Error testing provider connection",
                    ErrorDetails = ex.Message,
                    ProviderName = providerCredential?.ProviderName ?? "Unknown"
                };
            }
        }

        #endregion

        #region IP Filters

        /// <inheritdoc />
        public async Task<IEnumerable<IpFilterDto>> GetAllIpFiltersAsync()
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.GetAsync("api/ipfilter");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<IpFilterDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<IpFilterDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving IP filters from Admin API");
                return Enumerable.Empty<IpFilterDto>();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IpFilterDto>> GetEnabledIpFiltersAsync()
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.GetAsync("api/ipfilter/enabled");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<IpFilterDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<IpFilterDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enabled IP filters from Admin API");
                return Enumerable.Empty<IpFilterDto>();
            }
        }

        /// <inheritdoc />
        public async Task<IpFilterSettingsDto> GetIpFilterSettingsAsync()
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.GetAsync("api/ipfilter/settings");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IpFilterSettingsDto>(_jsonOptions);
                if (result == null)
                {
                    return new IpFilterSettingsDto
                    {
                        IsEnabled = false,
                        DefaultAllow = true,
                        BypassForAdminUi = true,
                        ExcludedEndpoints = new List<string> { "/api/v1/health" },
                        FilterMode = "permissive",
                        WhitelistFilters = new List<IpFilterDto>(),
                        BlacklistFilters = new List<IpFilterDto>()
                    };
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving IP filter settings from Admin API");
                return new IpFilterSettingsDto
                {
                    IsEnabled = false,
                    DefaultAllow = true,
                    BypassForAdminUi = true,
                    ExcludedEndpoints = new List<string> { "/api/v1/health" },
                    FilterMode = "permissive",
                    WhitelistFilters = new List<IpFilterDto>(),
                    BlacklistFilters = new List<IpFilterDto>()
                };
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateIpFilterSettingsAsync(IpFilterSettingsDto settings)
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.PutAsJsonAsync("api/ipfilter/settings", settings);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter settings in Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IpFilterDto?> GetIpFilterByIdAsync(int id)
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.GetAsync($"api/ipfilter/{id}");
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<IpFilterDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving IP filter with ID {FilterId} from Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IpFilterDto?> CreateIpFilterAsync(CreateIpFilterDto ipFilter)
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.PostAsJsonAsync("api/ipfilter", ipFilter);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<IpFilterDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating IP filter in Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IpFilterDto?> UpdateIpFilterAsync(int id, UpdateIpFilterDto ipFilter)
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.PutAsJsonAsync($"api/ipfilter/{id}", ipFilter);
                response.EnsureSuccessStatusCode();
                
                // Admin API returns 204 No Content for successful updates, not an IpFilterDto
                // So we need to fetch the updated record separately
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    // Fetch the updated IP filter to return it
                    return await GetIpFilterByIdAsync(id);
                }
                
                // Fallback in case the API changes to return content
                return await response.Content.ReadFromJsonAsync<IpFilterDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IP filter with ID {FilterId} in Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteIpFilterAsync(int id)
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.DeleteAsync($"api/ipfilter/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting IP filter with ID {FilterId} from Admin API", id);
                return false;
            }
        }

        #endregion

        #region Logs

        /// <inheritdoc />
        public async Task<ConduitLLM.Configuration.DTOs.PagedResult<RequestLogDto>?> GetRequestLogsAsync(
            int page = 1,
            int pageSize = 20,
            int? virtualKeyId = null,
            string? modelId = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            try
            {
                var queryParams = new List<string>
                {
                    $"page={page}",
                    $"pageSize={pageSize}"
                };

                if (virtualKeyId.HasValue)
                {
                    queryParams.Add($"virtualKeyId={virtualKeyId.Value}");
                }

                if (!string.IsNullOrEmpty(modelId))
                {
                    queryParams.Add($"modelId={Uri.EscapeDataString(modelId)}");
                }

                if (startDate.HasValue)
                {
                    queryParams.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("o"))}");
                }

                if (endDate.HasValue)
                {
                    queryParams.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("o"))}");
                }

                var url = $"api/logs?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<ConduitLLM.Configuration.DTOs.PagedResult<RequestLogDto>>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request logs from Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<RequestLogDto?> CreateRequestLogAsync(RequestLogDto logDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/logs", logDto);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<RequestLogDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request log in Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>> GetDailyUsageStatsAsync(
            DateTime startDate,
            DateTime endDate,
            int? virtualKeyId = null)
        {
            try
            {
                var queryParams = new List<string>
                {
                    $"startDate={Uri.EscapeDataString(startDate.ToString("o"))}",
                    $"endDate={Uri.EscapeDataString(endDate.ToString("o"))}"
                };

                if (virtualKeyId.HasValue)
                {
                    queryParams.Add($"virtualKeyId={virtualKeyId.Value}");
                }

                var url = $"api/logs/daily-stats?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>>(_jsonOptions);
                
                // Convert Configuration DTOs to WebUI DTOs
                if (result == null)
                {
                    return Enumerable.Empty<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>();
                }
                
                // Map the DTOs
                return result.Select(dto => new ConduitLLM.WebUI.DTOs.DailyUsageStatsDto
                {
                    Date = dto.Date,
                    RequestCount = dto.RequestCount,
                    InputTokens = dto.InputTokens,
                    OutputTokens = dto.OutputTokens,
                    Cost = dto.Cost,
                    ModelName = dto.ModelName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily usage statistics from Admin API");
                return Enumerable.Empty<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetDistinctModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/logs/models");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(_jsonOptions);
                return result ?? Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving distinct models from Admin API");
                return Enumerable.Empty<string>();
            }
        }

        /// <inheritdoc />
        public async Task<LogsSummaryDto?> GetLogsSummaryAsync(int days = 7, int? virtualKeyId = null)
        {
            try
            {
                string url = $"api/logs/summary?days={days}";
                if (virtualKeyId.HasValue)
                {
                    url += $"&virtualKeyId={virtualKeyId.Value}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<LogsSummaryDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs summary from Admin API");
                return null;
            }
        }

        #endregion

        #region Cost Dashboard

        /// <inheritdoc />
        public async Task<ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto?> GetCostDashboardAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                var queryParams = new List<string>();

                if (startDate.HasValue)
                {
                    queryParams.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("o"))}");
                }

                if (endDate.HasValue)
                {
                    queryParams.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("o"))}");
                }

                if (virtualKeyId.HasValue)
                {
                    queryParams.Add($"virtualKeyId={virtualKeyId.Value}");
                }

                if (!string.IsNullOrEmpty(modelName))
                {
                    queryParams.Add($"modelName={Uri.EscapeDataString(modelName)}");
                }

                var url = $"api/dashboard/costs?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cost dashboard data from Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<ConduitLLM.WebUI.DTOs.DetailedCostDataDto>?> GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                var queryParams = new List<string>();

                if (startDate.HasValue)
                {
                    queryParams.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("o"))}");
                }

                if (endDate.HasValue)
                {
                    queryParams.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("o"))}");
                }

                if (virtualKeyId.HasValue)
                {
                    queryParams.Add($"virtualKeyId={virtualKeyId.Value}");
                }

                if (!string.IsNullOrEmpty(modelName))
                {
                    queryParams.Add($"modelName={Uri.EscapeDataString(modelName)}");
                }

                var url = $"api/dashboard/costs/detailed?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var configResult = await response.Content.ReadFromJsonAsync<List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>>(_jsonOptions);
                
                // Convert from Configuration DTOs to WebUI DTOs
                if (configResult == null)
                {
                    return new List<ConduitLLM.WebUI.DTOs.DetailedCostDataDto>();
                }
                
                var webUiResult = configResult.Select(dto => new ConduitLLM.WebUI.DTOs.DetailedCostDataDto
                {
                    // Map Configuration DTO properties to WebUI DTO properties
                    Name = dto.Name,
                    Cost = dto.Cost,
                    Percentage = dto.Percentage
                }).ToList();
                
                return webUiResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving detailed cost data from Admin API");
                return new List<ConduitLLM.WebUI.DTOs.DetailedCostDataDto>();
            }
        }

        #endregion

        #region Router

        /// <inheritdoc />
        public async Task<ConduitLLM.Core.Models.Routing.RouterConfig?> GetRouterConfigAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/router/config");
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<ConduitLLM.Core.Models.Routing.RouterConfig>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving router configuration from Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateRouterConfigAsync(ConduitLLM.Core.Models.Routing.RouterConfig config)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("api/router/config", config);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating router configuration in Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<List<ConduitLLM.Core.Models.Routing.ModelDeployment>> GetAllModelDeploymentsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/router/deployments");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<List<ConduitLLM.Core.Models.Routing.ModelDeployment>>(_jsonOptions);
                return result ?? new List<ConduitLLM.Core.Models.Routing.ModelDeployment>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model deployments from Admin API");
                return new List<ConduitLLM.Core.Models.Routing.ModelDeployment>();
            }
        }

        /// <inheritdoc />
        public async Task<ConduitLLM.Core.Models.Routing.ModelDeployment?> GetModelDeploymentAsync(string modelName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/router/deployments/{Uri.EscapeDataString(modelName)}");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ConduitLLM.Core.Models.Routing.ModelDeployment>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model deployment for model {ModelName} from Admin API", modelName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveModelDeploymentAsync(ConduitLLM.Core.Models.Routing.ModelDeployment deployment)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/router/deployments", deployment);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving model deployment in Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelDeploymentAsync(string modelName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/router/deployments/{Uri.EscapeDataString(modelName)}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model deployment for model {ModelName} from Admin API", modelName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<List<ConduitLLM.Core.Models.Routing.FallbackConfiguration>> GetAllFallbackConfigurationsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/router/fallbacks");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<List<ConduitLLM.Core.Models.Routing.FallbackConfiguration>>(_jsonOptions);
                return result ?? new List<ConduitLLM.Core.Models.Routing.FallbackConfiguration>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fallback configurations from Admin API");
                return new List<ConduitLLM.Core.Models.Routing.FallbackConfiguration>();
            }
        }

        /// <inheritdoc />
        public async Task<bool> SetFallbackConfigurationAsync(ConduitLLM.Core.Models.Routing.FallbackConfiguration fallbackConfig)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/router/fallbacks", fallbackConfig);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting fallback configuration in Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveFallbackConfigurationAsync(string modelName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/router/fallbacks/{Uri.EscapeDataString(modelName)}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing fallback configuration for model {ModelName} from Admin API", modelName);
                return false;
            }
        }

        #endregion

        #region IRouterService implementation

        /// <summary>
        /// Gets the current router instance
        /// </summary>
        /// <returns>The router instance or null if not configured</returns>
        public ConduitLLM.Core.Interfaces.ILLMRouter? GetRouter()
        {
            // WebUI doesn't manage router instances directly - this is handled by the Admin API
            return null;
        }

        /// <summary>
        /// Gets all model deployments configured in the router
        /// </summary>
        /// <returns>List of model deployments</returns>
        public async Task<List<ConduitLLM.Core.Models.Routing.ModelDeployment>> GetModelDeploymentsAsync()
        {
            return await GetAllModelDeploymentsAsync();
        }

        /// <summary>
        /// Gets fallback configurations as a dictionary (IRouterService format)
        /// </summary>
        /// <returns>Dictionary of fallback configurations</returns>
        public async Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync()
        {
            var fallbacks = await GetAllFallbackConfigurationsAsync();
            var result = new Dictionary<string, List<string>>();
            
            foreach (var fallback in fallbacks)
            {
                if (!string.IsNullOrEmpty(fallback.PrimaryModelDeploymentId))
                {
                    result[fallback.PrimaryModelDeploymentId] = fallback.FallbackModelDeploymentIds ?? new List<string>();
                }
            }
            
            return result;
        }

        /// <summary>
        /// Sets a fallback configuration (IRouterService format)
        /// </summary>
        /// <param name="primaryModel">The primary model name</param>
        /// <param name="fallbackModels">List of fallback model names</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels)
        {
            var fallbackConfig = new ConduitLLM.Core.Models.Routing.FallbackConfiguration
            {
                PrimaryModelDeploymentId = primaryModel,
                FallbackModelDeploymentIds = fallbackModels
            };
            
            return await SetFallbackConfigurationAsync(fallbackConfig);
        }

        /// <summary>
        /// Initializes the router from configuration
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task InitializeRouterAsync()
        {
            // Router initialization is handled by the Admin API
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets the current router status including configuration and enabled state
        /// </summary>
        /// <returns>A RouterStatus object containing the configuration and enabled state</returns>  
        public async Task<ConduitLLM.WebUI.Interfaces.RouterStatus> GetRouterStatusAsync()
        {
            try
            {
                var config = await GetRouterConfigAsync();
                return new ConduitLLM.WebUI.Interfaces.RouterStatus
                {
                    Config = config,
                    IsEnabled = config != null && config.FallbacksEnabled
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting router status from Admin API");
                return new ConduitLLM.WebUI.Interfaces.RouterStatus
                {
                    Config = null,
                    IsEnabled = false
                };
            }
        }

        #endregion

        #region Database Backup

        /// <inheritdoc />
        public async Task<bool> CreateDatabaseBackupAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("api/database/backup", new StringContent(string.Empty));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database backup in Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<string> GetDatabaseBackupDownloadUrl()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/database/backup/download");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving database backup download URL from Admin API");
                return string.Empty;
            }
        }

        /// <inheritdoc />
        public async Task<object> GetSystemInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/systeminfo/info");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<object>(_jsonOptions);
                return result ?? new { };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system information from Admin API");
                return new { Error = "Failed to retrieve system information" };
            }
        }

        #endregion

        #region Explicit Interface Implementations

        /// <summary>
        /// Explicit implementation for IRouterService.GetRouterConfigAsync to handle non-nullable return type
        /// </summary>
        async Task<ConduitLLM.Core.Models.Routing.RouterConfig> ConduitLLM.WebUI.Interfaces.IRouterService.GetRouterConfigAsync()
        {
            var result = await GetRouterConfigAsync();
            return result ?? new ConduitLLM.Core.Models.Routing.RouterConfig();
        }

        #endregion

        #region IProviderCredentialService Implementation

        /// <summary>
        /// Gets all provider credentials.
        /// </summary>
        /// <returns>Collection of provider credentials.</returns>
        async Task<IEnumerable<ProviderCredentialDto>> ConduitLLM.WebUI.Interfaces.IProviderCredentialService.GetAllAsync()
        {
            return await GetAllProviderCredentialsAsync();
        }

        /// <summary>
        /// Gets a provider credential by ID.
        /// </summary>
        /// <param name="id">The ID of the provider credential to retrieve.</param>
        /// <returns>The provider credential, or null if not found.</returns>
        async Task<ProviderCredentialDto?> ConduitLLM.WebUI.Interfaces.IProviderCredentialService.GetByIdAsync(int id)
        {
            return await GetProviderCredentialByIdAsync(id);
        }

        /// <summary>
        /// Gets a provider credential by provider name.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider credential, or null if not found.</returns>
        async Task<ProviderCredentialDto?> ConduitLLM.WebUI.Interfaces.IProviderCredentialService.GetByProviderNameAsync(string providerName)
        {
            return await GetProviderCredentialByNameAsync(providerName);
        }

        /// <summary>
        /// Creates a new provider credential.
        /// </summary>
        /// <param name="credential">The provider credential to create.</param>
        /// <returns>The created provider credential.</returns>
        async Task<ProviderCredentialDto?> ConduitLLM.WebUI.Interfaces.IProviderCredentialService.CreateAsync(CreateProviderCredentialDto credential)
        {
            return await CreateProviderCredentialAsync(credential);
        }

        /// <summary>
        /// Updates a provider credential.
        /// </summary>
        /// <param name="id">The ID of the provider credential to update.</param>
        /// <param name="credential">The updated provider credential.</param>
        /// <returns>The updated provider credential, or null if the update failed.</returns>
        async Task<ProviderCredentialDto?> ConduitLLM.WebUI.Interfaces.IProviderCredentialService.UpdateAsync(int id, UpdateProviderCredentialDto credential)
        {
            return await UpdateProviderCredentialAsync(id, credential);
        }

        /// <summary>
        /// Deletes a provider credential.
        /// </summary>
        /// <param name="id">The ID of the provider credential to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        async Task<bool> ConduitLLM.WebUI.Interfaces.IProviderCredentialService.DeleteAsync(int id)
        {
            return await DeleteProviderCredentialAsync(id);
        }

        /// <summary>
        /// Tests a provider connection.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>A result indicating whether the connection was successful.</returns>
        async Task<ProviderConnectionTestResultDto?> ConduitLLM.WebUI.Interfaces.IProviderCredentialService.TestConnectionAsync(string providerName)
        {
            return await TestProviderConnectionAsync(providerName);
        }

        #endregion
    }
}