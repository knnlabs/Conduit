using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Partial class for audio-related admin API operations.
    /// </summary>
    public partial class AdminApiClient
    {
        #region Audio Provider Configuration

        /// <summary>
        /// Gets all audio provider configurations.
        /// </summary>
        /// <returns>List of audio provider configurations</returns>
        public async Task<List<AudioProviderConfigDto>> GetAudioProvidersAsync()
        {
            var result = await ExecuteWithErrorHandlingAsync(
                "GetAudioProviders",
                async () =>
                {
                    var response = await _httpClient.GetFromJsonAsync<List<AudioProviderConfigDto>>(
                        "api/admin/audio/providers");
                    return response ?? new List<AudioProviderConfigDto>();
                },
                new List<AudioProviderConfigDto>());
            
            return result ?? new List<AudioProviderConfigDto>();
        }

        /// <summary>
        /// Gets a specific audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <returns>The audio provider configuration</returns>
        public async Task<AudioProviderConfigDto?> GetAudioProviderAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<AudioProviderConfigDto>(
                    $"api/admin/audio/providers/{id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audio provider {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Gets audio provider configurations by provider name.
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <returns>List of configurations for the provider</returns>
        public async Task<List<AudioProviderConfigDto>> GetAudioProvidersByNameAsync(string providerName)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<AudioProviderConfigDto>>(
                    $"api/admin/audio/providers/by-name/{providerName}");
                return response ?? new List<AudioProviderConfigDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audio providers by name {ProviderName}", providerName);
                throw;
            }
        }

        /// <summary>
        /// Gets enabled providers for a specific audio operation.
        /// </summary>
        /// <param name="operationType">The operation type (transcription, tts, realtime)</param>
        /// <returns>List of enabled providers</returns>
        public async Task<List<AudioProviderConfigDto>> GetEnabledAudioProvidersAsync(string operationType)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<AudioProviderConfigDto>>(
                    $"api/admin/audio/providers/enabled/{operationType}");
                return response ?? new List<AudioProviderConfigDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching enabled audio providers for {OperationType}", operationType);
                throw;
            }
        }

        /// <summary>
        /// Creates a new audio provider configuration.
        /// </summary>
        /// <param name="providerConfig">The provider configuration to create</param>
        /// <returns>The created provider configuration</returns>
        public async Task<AudioProviderConfigDto> CreateAudioProviderAsync(AudioProviderConfigDto providerConfig)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/admin/audio/providers", providerConfig);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AudioProviderConfigDto>()
                    ?? throw new InvalidOperationException("Invalid response from server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audio provider");
                throw;
            }
        }

        /// <summary>
        /// Updates an audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <param name="providerConfig">The updated configuration</param>
        /// <returns>The updated provider configuration</returns>
        public async Task<AudioProviderConfigDto?> UpdateAudioProviderAsync(int id, AudioProviderConfigDto providerConfig)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync(
                    $"api/admin/audio/providers/{id}", providerConfig);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AudioProviderConfigDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating audio provider {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Deletes an audio provider configuration.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteAudioProviderAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/admin/audio/providers/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting audio provider {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Tests audio provider connectivity.
        /// </summary>
        /// <param name="id">The provider configuration ID</param>
        /// <param name="operationType">The operation type to test</param>
        /// <returns>The test results</returns>
        public async Task<AudioProviderTestResult> TestAudioProviderAsync(int id, string operationType = "transcription")
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"api/admin/audio/providers/{id}/test?operationType={operationType}", null);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AudioProviderTestResult>()
                    ?? throw new InvalidOperationException("Invalid response from server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing audio provider {Id}", id);
                throw;
            }
        }

        #endregion

        #region Audio Cost Configuration

        /// <summary>
        /// Gets all audio cost configurations.
        /// </summary>
        /// <returns>List of audio cost configurations</returns>
        public async Task<List<AudioCostDto>> GetAudioCostsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<AudioCostDto>>(
                    "api/admin/audio/costs");
                return response ?? new List<AudioCostDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audio costs");
                throw;
            }
        }

        /// <summary>
        /// Gets a specific audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <returns>The audio cost configuration</returns>
        public async Task<AudioCostDto?> GetAudioCostAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<AudioCostDto>(
                    $"api/admin/audio/costs/{id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audio cost {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Gets audio costs by provider.
        /// </summary>
        /// <param name="provider">The provider name</param>
        /// <returns>List of costs for the provider</returns>
        public async Task<List<AudioCostDto>> GetAudioCostsByProviderAsync(string provider)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<AudioCostDto>>(
                    $"api/admin/audio/costs/by-provider/{provider}");
                return response ?? new List<AudioCostDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audio costs for provider {Provider}", provider);
                throw;
            }
        }

        /// <summary>
        /// Gets the current cost for a specific operation.
        /// </summary>
        /// <param name="provider">The provider name</param>
        /// <param name="operationType">The operation type</param>
        /// <param name="model">The model name (optional)</param>
        /// <returns>The current cost</returns>
        public async Task<AudioCostDto?> GetCurrentAudioCostAsync(string provider, string operationType, string? model = null)
        {
            try
            {
                var query = $"?provider={provider}&operationType={operationType}";
                if (!string.IsNullOrEmpty(model))
                    query += $"&model={model}";

                return await _httpClient.GetFromJsonAsync<AudioCostDto>(
                    $"api/admin/audio/costs/current{query}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current audio cost");
                throw;
            }
        }

        /// <summary>
        /// Creates a new audio cost configuration.
        /// </summary>
        /// <param name="cost">The cost configuration to create</param>
        /// <returns>The created cost configuration</returns>
        public async Task<AudioCostDto> CreateAudioCostAsync(AudioCostDto cost)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/admin/audio/costs", cost);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AudioCostDto>()
                    ?? throw new InvalidOperationException("Invalid response from server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audio cost");
                throw;
            }
        }

        /// <summary>
        /// Updates an audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <param name="cost">The updated configuration</param>
        /// <returns>The updated cost configuration</returns>
        public async Task<AudioCostDto?> UpdateAudioCostAsync(int id, AudioCostDto cost)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync(
                    $"api/admin/audio/costs/{id}", cost);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AudioCostDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating audio cost {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Deletes an audio cost configuration.
        /// </summary>
        /// <param name="id">The cost configuration ID</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteAudioCostAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/admin/audio/costs/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting audio cost {Id}", id);
                throw;
            }
        }

        #endregion

        #region Audio Usage Analytics

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
        public async Task<PagedResult<AudioUsageDto>> GetAudioUsageLogsAsync(
            int pageNumber = 1,
            int pageSize = 50,
            string? virtualKey = null,
            string? provider = null,
            string? operationType = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            try
            {
                var query = $"?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(virtualKey))
                    query += $"&virtualKey={virtualKey}";
                if (!string.IsNullOrEmpty(provider))
                    query += $"&provider={provider}";
                if (!string.IsNullOrEmpty(operationType))
                    query += $"&operationType={operationType}";
                if (startDate.HasValue)
                    query += $"&startDate={startDate.Value:yyyy-MM-dd}";
                if (endDate.HasValue)
                    query += $"&endDate={endDate.Value:yyyy-MM-dd}";

                var response = await _httpClient.GetFromJsonAsync<PagedResult<AudioUsageDto>>(
                    $"api/admin/audio/usage{query}");
                return response ?? new PagedResult<AudioUsageDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audio usage logs");
                throw;
            }
        }

        /// <summary>
        /// Gets audio usage summary statistics.
        /// </summary>
        /// <param name="startDate">Start date for the summary</param>
        /// <param name="endDate">End date for the summary</param>
        /// <param name="virtualKey">Filter by virtual key (optional)</param>
        /// <param name="provider">Filter by provider (optional)</param>
        /// <returns>Usage summary</returns>
        public async Task<ConduitLLM.Configuration.DTOs.Audio.AudioUsageSummaryDto> GetAudioUsageSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            string? virtualKey = null,
            string? provider = null)
        {
            try
            {
                var query = $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
                if (!string.IsNullOrEmpty(virtualKey))
                    query += $"&virtualKey={virtualKey}";
                if (!string.IsNullOrEmpty(provider))
                    query += $"&provider={provider}";

                var response = await _httpClient.GetFromJsonAsync<ConduitLLM.Configuration.DTOs.Audio.AudioUsageSummaryDto>(
                    $"api/admin/audio/usage/summary{query}");
                return response ?? new ConduitLLM.Configuration.DTOs.Audio.AudioUsageSummaryDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audio usage summary");
                throw;
            }
        }

        /// <summary>
        /// Exports audio usage data in the specified format.
        /// </summary>
        /// <param name="pageNumber">Page number (set to 1 for export)</param>
        /// <param name="pageSize">Page size (set to large number for full export)</param>
        /// <param name="virtualKey">Filter by virtual key (optional)</param>
        /// <param name="provider">Filter by provider (optional)</param>
        /// <param name="operationType">Filter by operation type (optional)</param>
        /// <param name="startDate">Start date filter (optional)</param>
        /// <param name="endDate">End date filter (optional)</param>
        /// <param name="format">Export format (csv or json)</param>
        /// <returns>Exported data as a byte array</returns>
        public async Task<byte[]> ExportAudioUsageDataAsync(
            int pageNumber = 1,
            int pageSize = int.MaxValue,
            string? virtualKey = null,
            string? provider = null,
            string? operationType = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string format = "csv")
        {
            try
            {
                var query = $"?page={pageNumber}&pageSize={pageSize}&format={format}";
                if (!string.IsNullOrEmpty(virtualKey))
                    query += $"&virtualKey={virtualKey}";
                if (!string.IsNullOrEmpty(provider))
                    query += $"&provider={provider}";
                if (!string.IsNullOrEmpty(operationType))
                    query += $"&operationType={operationType}";
                if (startDate.HasValue)
                    query += $"&startDate={startDate.Value:yyyy-MM-dd}";
                if (endDate.HasValue)
                    query += $"&endDate={endDate.Value:yyyy-MM-dd}";

                var response = await _httpClient.GetAsync($"api/admin/audio/usage/export{query}");
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audio usage data in format {Format}", format);
                throw;
            }
        }

        #endregion

        #region Real-time Session Management

        /// <summary>
        /// Gets real-time session metrics.
        /// </summary>
        /// <returns>Session metrics</returns>
        public async Task<RealtimeSessionMetricsDto> GetRealtimeSessionMetricsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<RealtimeSessionMetricsDto>(
                    "api/admin/audio/sessions/metrics");
                return response ?? new RealtimeSessionMetricsDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching realtime session metrics");
                throw;
            }
        }

        /// <summary>
        /// Gets active real-time sessions.
        /// </summary>
        /// <returns>List of active sessions</returns>
        public async Task<List<RealtimeSessionDto>> GetActiveRealtimeSessionsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<RealtimeSessionDto>>(
                    "api/admin/audio/sessions");
                return response ?? new List<RealtimeSessionDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active realtime sessions");
                throw;
            }
        }

        /// <summary>
        /// Gets details of a specific real-time session.
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>Session details</returns>
        public async Task<RealtimeSessionDto?> GetRealtimeSessionDetailsAsync(string sessionId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<RealtimeSessionDto>(
                    $"api/admin/audio/sessions/{sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching realtime session details {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Terminates an active real-time session.
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>True if terminated successfully</returns>
        public async Task<bool> TerminateRealtimeSessionAsync(string sessionId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/admin/audio/sessions/{sessionId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating realtime session {SessionId}", sessionId);
                throw;
            }
        }

        #endregion
    }

    #region Supporting DTOs

    /// <summary>
    /// Result of testing an audio provider.
    /// </summary>
    public class AudioProviderTestResult
    {
        /// <summary>
        /// Gets or sets whether the test was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the test message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response time in milliseconds.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets any error details.
        /// </summary>
        public string? ErrorDetails { get; set; }
    }


    /// <summary>
    /// Metrics for real-time sessions.
    /// </summary>
    public class RealtimeSessionMetricsDto
    {
        /// <summary>
        /// Gets or sets the number of active sessions.
        /// </summary>
        public int ActiveSessions { get; set; }

        /// <summary>
        /// Gets or sets the total sessions today.
        /// </summary>
        public int TotalSessionsToday { get; set; }

        /// <summary>
        /// Gets or sets the average session duration in minutes.
        /// </summary>
        public double AverageSessionDurationMinutes { get; set; }

        /// <summary>
        /// Gets or sets the total cost today.
        /// </summary>
        public decimal TotalCostToday { get; set; }

        /// <summary>
        /// Gets or sets provider distribution.
        /// </summary>
        public Dictionary<string, int> ProviderDistribution { get; set; } = new();
    }

    #endregion
}
