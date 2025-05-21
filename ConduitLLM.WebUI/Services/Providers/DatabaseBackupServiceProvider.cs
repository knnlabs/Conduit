using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IDatabaseBackupService that uses IAdminApiClient to interact with the Admin API.
    /// </summary>
    public class DatabaseBackupServiceProvider : IDatabaseBackupService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<DatabaseBackupServiceProvider> _logger;
        
        /// <summary>
        /// Initializes a new instance of the DatabaseBackupServiceProvider class
        /// </summary>
        /// <param name="adminApiClient">The Admin API client</param>
        /// <param name="logger">The logger</param>
        public DatabaseBackupServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<DatabaseBackupServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public async Task<byte[]> CreateBackupAsync()
        {
            try
            {
                // Create a backup through the Admin API
                var success = await _adminApiClient.CreateDatabaseBackupAsync();
                
                if (!success)
                {
                    _logger.LogWarning("Failed to create database backup through Admin API");
                    return Array.Empty<byte>();
                }
                
                // Download the backup file
                var downloadUrl = await _adminApiClient.GetDatabaseBackupDownloadUrl();
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database backup through Admin API");
                return Array.Empty<byte>();
            }
        }
        
        /// <inheritdoc />
        public async Task<bool> RestoreFromBackupAsync(byte[] backupData)
        {
            // The current Admin API doesn't support direct restoration from byte array.
            // For a complete implementation, we would need to:
            // 1. Upload the byte array to a temporary location
            // 2. Call an Admin API endpoint to restore from that location
            // 3. Clean up the temporary file
            
            _logger.LogWarning("Restore from backup is not currently supported through the Admin API");
            
            // Add await to make this properly async
            await Task.CompletedTask;
            
            return false;
        }
        
        /// <inheritdoc />
        public async Task<bool> ValidateBackupAsync(byte[] backupData)
        {
            try
            {
                // Add await to make this properly async
                await Task.CompletedTask;
                
                // Implement basic validation based on the backup format
                // For a SQLite backup, check if it looks like a SQLite file
                if (backupData.Length > 16)
                {
                    // Check for SQLite header magic string "SQLite format 3\0"
                    if (backupData[0] == 'S' && backupData[1] == 'Q' && backupData[2] == 'L' && 
                        backupData[3] == 'i' && backupData[4] == 't' && backupData[5] == 'e')
                    {
                        return true;
                    }
                }
                
                // For other database types, we might need more sophisticated validation
                // For now, just check if the backup data contains something
                return backupData.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating backup data");
                return false;
            }
        }
        
        /// <inheritdoc />
        public string GetDatabaseProvider()
        {
            try
            {
                // Get this from the system info
                var systemInfoTask = _adminApiClient.GetSystemInfoAsync();
                systemInfoTask.Wait();
                var systemInfo = systemInfoTask.Result;
                
                // Try to extract database provider from the system info
                if (systemInfo is JsonElement jsonElement && 
                    jsonElement.TryGetProperty("database", out var dbInfo) &&
                    dbInfo.TryGetProperty("provider", out var providerInfo))
                {
                    return providerInfo.GetString()?.ToLowerInvariant() ?? "unknown";
                }
                
                // If we can't find it in system info, return a default value
                _logger.LogWarning("Unable to determine database provider from system info");
                return "unknown";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database provider from Admin API");
                return "unknown";
            }
        }
    }
}