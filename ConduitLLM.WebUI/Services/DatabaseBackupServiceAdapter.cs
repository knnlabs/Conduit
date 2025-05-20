using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for database backup operations that can use either direct repository access or the Admin API
/// </summary>
public class DatabaseBackupServiceAdapter : IDatabaseBackupService
{
    private readonly DatabaseBackupService _repositoryService;
    private readonly IAdminApiClient _adminApiClient;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly ILogger<DatabaseBackupServiceAdapter> _logger;
    
    /// <summary>
    /// Initializes a new instance of the DatabaseBackupServiceAdapter class
    /// </summary>
    /// <param name="repositoryService">The repository-based database backup service</param>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="adminApiOptions">The Admin API options</param>
    /// <param name="logger">The logger</param>
    public DatabaseBackupServiceAdapter(
        DatabaseBackupService repositoryService,
        IAdminApiClient adminApiClient,
        IOptions<AdminApiOptions> adminApiOptions,
        ILogger<DatabaseBackupServiceAdapter> logger)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<byte[]> CreateBackupAsync()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // Create a backup through the Admin API
                var success = await _adminApiClient.CreateDatabaseBackupAsync();
                
                if (!success)
                {
                    _logger.LogWarning("Failed to create database backup through Admin API, falling back to repository");
                    return await _repositoryService.CreateBackupAsync();
                }
                
                // We need to download the backup file since the API just returns metadata
                var downloadUrl = await _adminApiClient.GetDatabaseBackupDownloadUrl();
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database backup through Admin API, falling back to repository");
                return await _repositoryService.CreateBackupAsync();
            }
        }
        
        return await _repositoryService.CreateBackupAsync();
    }
    
    /// <inheritdoc />
    public async Task<bool> RestoreFromBackupAsync(byte[] backupData)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // We need to handle this differently since the Admin API doesn't support
                // direct restoration from byte array. We would need to upload the backup
                // first and then restore from it.
                
                // For now, default to the repository implementation
                _logger.LogWarning("Admin API does not support direct restoration from byte array, falling back to repository");
                return await _repositoryService.RestoreFromBackupAsync(backupData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring database from backup through Admin API, falling back to repository");
                return await _repositoryService.RestoreFromBackupAsync(backupData);
            }
        }
        
        return await _repositoryService.RestoreFromBackupAsync(backupData);
    }
    
    /// <inheritdoc />
    public async Task<bool> ValidateBackupAsync(byte[] backupData)
    {
        // Always use the repository implementation for validation since it needs
        // direct access to the backup data
        return await _repositoryService.ValidateBackupAsync(backupData);
    }
    
    /// <inheritdoc />
    public string GetDatabaseProvider()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // We can get this from the system info
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
                
                _logger.LogWarning("Unable to get database provider from system info, falling back to repository");
                return _repositoryService.GetDatabaseProvider();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database provider from Admin API, falling back to repository");
                return _repositoryService.GetDatabaseProvider();
            }
        }
        
        return _repositoryService.GetDatabaseProvider();
    }
}