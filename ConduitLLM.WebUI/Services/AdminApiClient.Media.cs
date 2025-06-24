using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Media management methods for the AdminApiClient.
    /// </summary>
    public partial class AdminApiClient
    {
        /// <inheritdoc/>
        public async Task<OverallMediaStorageStats?> GetOverallMediaStatsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/admin/media/stats");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<OverallMediaStorageStats>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overall media statistics");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<MediaStorageStats?> GetMediaStatsByVirtualKeyAsync(int virtualKeyId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/admin/media/stats/virtual-key/{virtualKeyId}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<MediaStorageStats>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media statistics for virtual key {VirtualKeyId}", virtualKeyId);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>?> GetMediaStatsByProviderAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/admin/media/stats/by-provider");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<Dictionary<string, long>>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media statistics by provider");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>?> GetMediaStatsByMediaTypeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/admin/media/stats/by-type");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<Dictionary<string, long>>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media statistics by media type");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>?> GetMediaByVirtualKeyAsync(int virtualKeyId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/admin/media/virtual-key/{virtualKeyId}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<List<MediaRecord>>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media for virtual key {VirtualKeyId}", virtualKeyId);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>?> SearchMediaAsync(string pattern)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    throw new ArgumentException("Search pattern cannot be empty", nameof(pattern));
                }

                var encodedPattern = Uri.EscapeDataString(pattern);
                var response = await _httpClient.GetAsync($"api/admin/media/search?pattern={encodedPattern}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<List<MediaRecord>>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching media with pattern {Pattern}", pattern);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteMediaAsync(Guid mediaId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/admin/media/{mediaId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting media {MediaId}", mediaId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CleanupExpiredMediaAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("api/admin/media/cleanup/expired", null);
                if (!response.IsSuccessStatusCode)
                {
                    return 0;
                }
                
                var result = await response.Content.ReadFromJsonAsync<CleanupResponse>(_jsonOptions);
                return result?.DeletedCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired media");
                return 0;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CleanupOrphanedMediaAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("api/admin/media/cleanup/orphaned", null);
                if (!response.IsSuccessStatusCode)
                {
                    return 0;
                }
                
                var result = await response.Content.ReadFromJsonAsync<CleanupResponse>(_jsonOptions);
                return result?.DeletedCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up orphaned media");
                return 0;
            }
        }

        /// <inheritdoc/>
        public async Task<int> PruneOldMediaAsync(int daysToKeep)
        {
            try
            {
                var request = new { DaysToKeep = daysToKeep };
                var response = await _httpClient.PostAsJsonAsync("api/admin/media/cleanup/prune", request, _jsonOptions);
                if (!response.IsSuccessStatusCode)
                {
                    return 0;
                }
                
                var result = await response.Content.ReadFromJsonAsync<CleanupResponse>(_jsonOptions);
                return result?.DeletedCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pruning old media");
                return 0;
            }
        }

        /// <summary>
        /// Response model for cleanup operations.
        /// </summary>
        private class CleanupResponse
        {
            /// <summary>
            /// Gets or sets the message.
            /// </summary>
            public string? Message { get; set; }

            /// <summary>
            /// Gets or sets the number of items deleted.
            /// </summary>
            public int DeletedCount { get; set; }
        }
    }
}