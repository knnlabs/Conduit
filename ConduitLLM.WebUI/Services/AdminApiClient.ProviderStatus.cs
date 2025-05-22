using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using System.Text.Json;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient : IProviderHealthService
    {
        #region IAdminApiClient Members

        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/providerhealth/status");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, ProviderStatus>>(_jsonOptions);
                return result ?? new Dictionary<string, ProviderStatus>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking all providers status");
                return new Dictionary<string, ProviderStatus>();
            }
        }
        
        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/providerhealth/status/{Uri.EscapeDataString(providerName)}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new ProviderStatus { Status = ProviderStatus.StatusType.Offline, StatusMessage = "Provider not found" };
                }
                
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<ProviderStatus>(_jsonOptions);
                return result ?? new ProviderStatus { Status = ProviderStatus.StatusType.Unknown, StatusMessage = "Unknown error" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking provider status for {ProviderName}", providerName);
                return new ProviderStatus { Status = ProviderStatus.StatusType.Offline, StatusMessage = ex.Message };
            }
        }

        /// <inheritdoc />
        public async Task<int> PurgeOldProviderHealthRecordsAsync(DateTime olderThan)
        {
            try
            {
                // Calculate days from olderThan to now
                var days = (DateTime.UtcNow - olderThan).TotalDays;
                days = Math.Max(1, Math.Ceiling(days)); // Ensure at least 1 day
                
                var response = await _httpClient.DeleteAsync($"api/providerhealth/purge?days={days}");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<int>(_jsonOptions);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging old provider health records");
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SaveProviderHealthStatusAsync(ProviderHealthRecordDto status)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/providerhealth/check/{Uri.EscapeDataString(status.ProviderName)}", status);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving provider health status for {ProviderName}", status.ProviderName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, double>> GetProviderUptimeAsync(DateTime since)
        {
            try
            {
                // Convert to hours - the API uses hours parameter
                var hours = (DateTime.UtcNow - since).TotalHours;
                hours = Math.Max(1, Math.Ceiling(hours)); // Ensure at least 1 hour
                
                var response = await _httpClient.GetAsync($"api/providerhealth/statistics?hours={hours}");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(json);
                
                // Extract uptime percentages from the JSON response
                var uptimePercentages = new Dictionary<string, double>();
                
                if (result.RootElement.TryGetProperty("uptimePercentages", out var uptimeElement))
                {
                    foreach (var property in uptimeElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Number)
                        {
                            uptimePercentages[property.Name] = property.Value.GetDouble();
                        }
                    }
                }
                
                return uptimePercentages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider uptime since {Since}", since);
                return new Dictionary<string, double>();
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, double>> GetAverageResponseTimesAsync(DateTime since)
        {
            try
            {
                // Convert to hours - the API uses hours parameter
                var hours = (DateTime.UtcNow - since).TotalHours;
                hours = Math.Max(1, Math.Ceiling(hours)); // Ensure at least 1 hour
                
                var response = await _httpClient.GetAsync($"api/providerhealth/statistics?hours={hours}");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(json);
                
                // Extract average response times from the JSON response
                var averageResponseTimes = new Dictionary<string, double>();
                
                if (result.RootElement.TryGetProperty("averageResponseTimes", out var timesElement))
                {
                    foreach (var property in timesElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Number)
                        {
                            averageResponseTimes[property.Name] = property.Value.GetDouble();
                        }
                    }
                }
                
                return averageResponseTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting average response times since {Since}", since);
                return new Dictionary<string, double>();
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, int>> GetErrorCountByProviderAsync(DateTime since)
        {
            try
            {
                // Convert to hours - the API uses hours parameter
                var hours = (DateTime.UtcNow - since).TotalHours;
                hours = Math.Max(1, Math.Ceiling(hours)); // Ensure at least 1 hour
                
                var response = await _httpClient.GetAsync($"api/providerhealth/statistics?hours={hours}");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(json);
                
                // Extract error counts from the JSON response
                var errorCounts = new Dictionary<string, int>();
                
                if (result.RootElement.TryGetProperty("errorCounts", out var countsElement))
                {
                    foreach (var property in countsElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Number)
                        {
                            errorCounts[property.Name] = property.Value.GetInt32();
                        }
                    }
                }
                
                return errorCounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting error count by provider since {Since}", since);
                return new Dictionary<string, int>();
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, Dictionary<string, int>>> GetErrorCategoriesByProviderAsync(DateTime since)
        {
            try
            {
                // Convert to hours - the API uses hours parameter
                var hours = (DateTime.UtcNow - since).TotalHours;
                hours = Math.Max(1, Math.Ceiling(hours)); // Ensure at least 1 hour
                
                var response = await _httpClient.GetAsync($"api/providerhealth/statistics?hours={hours}");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(json);
                
                // Extract error categories from the JSON response
                var errorCategories = new Dictionary<string, Dictionary<string, int>>();
                
                if (result.RootElement.TryGetProperty("errorCategories", out var categoriesElement))
                {
                    foreach (var providerProperty in categoriesElement.EnumerateObject())
                    {
                        var providerName = providerProperty.Name;
                        var categoryDict = new Dictionary<string, int>();
                        
                        if (providerProperty.Value.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var categoryProperty in providerProperty.Value.EnumerateObject())
                            {
                                if (categoryProperty.Value.ValueKind == JsonValueKind.Number)
                                {
                                    categoryDict[categoryProperty.Name] = categoryProperty.Value.GetInt32();
                                }
                            }
                        }
                        
                        errorCategories[providerName] = categoryDict;
                    }
                }
                
                return errorCategories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting error categories by provider since {Since}", since);
                return new Dictionary<string, Dictionary<string, int>>();
            }
        }

        /// <inheritdoc />
        public async Task<int> GetConsecutiveFailuresAsync(string providerName, DateTime since)
        {
            try
            {
                // Get the history for the provider
                var hours = (DateTime.UtcNow - since).TotalHours;
                hours = Math.Max(1, Math.Ceiling(hours)); // Ensure at least 1 hour
                
                var response = await _httpClient.GetAsync($"api/providerhealth/history/{Uri.EscapeDataString(providerName)}?hours={hours}&limit=100");
                response.EnsureSuccessStatusCode();
                
                var history = await response.Content.ReadFromJsonAsync<List<ProviderHealthRecordDto>>(_jsonOptions);
                if (history == null || !history.Any())
                {
                    return 0;
                }
                
                // Count consecutive failures starting from the most recent
                int consecutiveFailures = 0;
                foreach (var record in history.OrderByDescending(r => r.TimestampUtc))
                {
                    if (!record.IsOnline)
                    {
                        consecutiveFailures++;
                    }
                    else
                    {
                        // Break on first successful status
                        break;
                    }
                }
                
                return consecutiveFailures;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting consecutive failures for provider {ProviderName} since {Since}", providerName, since);
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateLastCheckedTimeAsync(string providerName)
        {
            try
            {
                // We'll use the trigger health check API to update the last checked time
                await _httpClient.PostAsync($"api/providerhealth/check/{Uri.EscapeDataString(providerName)}", null);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last checked time for provider {ProviderName}", providerName);
                return false;
            }
        }
        
        #endregion
        
        #region IProviderHealthService Members
        
        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthConfigurationDto>> GetAllConfigurationsAsync()
        {
            return await GetAllProviderHealthConfigurationsAsync();
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> GetConfigurationByNameAsync(string providerName)
        {
            return await GetProviderHealthConfigurationByNameAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> CreateConfigurationAsync(ConfigDTOs.CreateProviderHealthConfigurationDto config)
        {
            return await CreateProviderHealthConfigurationAsync(config);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> UpdateConfigurationAsync(string providerName, ConfigDTOs.UpdateProviderHealthConfigurationDto config)
        {
            return await UpdateProviderHealthConfigurationAsync(providerName, config);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteConfigurationAsync(string providerName)
        {
            return await DeleteProviderHealthConfigurationAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthRecordDto>> GetHealthRecordsAsync(string? providerName = null)
        {
            return await GetProviderHealthRecordsAsync(providerName);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthSummaryDto>> GetHealthSummaryAsync()
        {
            return await GetProviderHealthSummaryAsync();
        }
        
        #endregion
    }
}