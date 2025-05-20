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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Client for interacting with the Admin API endpoints.
    /// </summary>
    public partial class AdminApiClient : IAdminApiClient
    {
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
            }

            // Configure timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(adminOptions.TimeoutSeconds);
            
            // Configure authentication headers
            if (!string.IsNullOrEmpty(adminOptions.MasterKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Master-Key", adminOptions.MasterKey);
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
        public async Task<IEnumerable<VirtualKeyCostDataDto>> GetVirtualKeyUsageStatisticsAsync(int? virtualKeyId = null)
        {
            try
            {
                string url = "api/virtualkeys/usage";
                if (virtualKeyId.HasValue)
                {
                    url += $"?virtualKeyId={virtualKeyId.Value}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<VirtualKeyCostDataDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<VirtualKeyCostDataDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key usage statistics from Admin API");
                return Enumerable.Empty<VirtualKeyCostDataDto>();
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
                var response = await _httpClient.GetAsync($"api/globalsettings/{Uri.EscapeDataString(key)}");
                
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
                var response = await _httpClient.PostAsJsonAsync("api/globalsettings", setting);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<GlobalSettingDto>(_jsonOptions);
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
                var response = await _httpClient.DeleteAsync($"api/globalsettings/{Uri.EscapeDataString(key)}");
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
                var response = await _httpClient.GetAsync("api/modelprovidermappings");
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
                var response = await _httpClient.GetAsync($"api/modelprovidermappings/{id}");

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
                var response = await _httpClient.GetAsync($"api/modelprovidermappings/by-alias/{Uri.EscapeDataString(modelAlias)}");

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
                var response = await _httpClient.PostAsJsonAsync("api/modelprovidermappings", mapping);
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
                var response = await _httpClient.PutAsJsonAsync($"api/modelprovidermappings/{id}", mapping);
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
                var response = await _httpClient.DeleteAsync($"api/modelprovidermappings/{id}");
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
                var response = await _httpClient.GetAsync($"api/providercredentials/by-name/{Uri.EscapeDataString(providerName)}");
                
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
                var response = await _httpClient.PostAsync(
                    $"api/providercredentials/test-connection/{Uri.EscapeDataString(providerName)}",
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

        #endregion

        #region IP Filters

        /// <inheritdoc />
        public async Task<IEnumerable<IpFilterDto>> GetAllIpFiltersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/ipfilters");
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
                var response = await _httpClient.GetAsync("api/ipfilters/enabled");
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
                var response = await _httpClient.GetAsync("api/ipfilters/settings");
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
                var response = await _httpClient.PutAsJsonAsync("api/ipfilters/settings", settings);
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
                var response = await _httpClient.GetAsync($"api/ipfilters/{id}");
                
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
                var response = await _httpClient.PostAsJsonAsync("api/ipfilters", ipFilter);
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
                var response = await _httpClient.PutAsJsonAsync($"api/ipfilters/{id}", ipFilter);
                response.EnsureSuccessStatusCode();
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
                var response = await _httpClient.DeleteAsync($"api/ipfilters/{id}");
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
        public async Task<IEnumerable<DailyUsageStatsDto>> GetDailyUsageStatsAsync(
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

                var result = await response.Content.ReadFromJsonAsync<IEnumerable<DailyUsageStatsDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<DailyUsageStatsDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily usage statistics from Admin API");
                return Enumerable.Empty<DailyUsageStatsDto>();
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
        public async Task<CostDashboardDto?> GetCostDashboardAsync(
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

                return await response.Content.ReadFromJsonAsync<CostDashboardDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cost dashboard data from Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<DetailedCostDataDto>?> GetDetailedCostDataAsync(
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

                var result = await response.Content.ReadFromJsonAsync<List<DetailedCostDataDto>>(_jsonOptions);
                return result ?? new List<DetailedCostDataDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving detailed cost data from Admin API");
                return new List<DetailedCostDataDto>();
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
                var response = await _httpClient.GetAsync("api/system/info");
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
    }
}