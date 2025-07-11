using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing database backups and restoration through the Admin API.
/// </summary>
public class DatabaseBackupService : BaseApiClient
{
    private const string BaseEndpoint = "/api/database";

    /// <summary>
    /// Initializes a new instance of the DatabaseBackupService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public DatabaseBackupService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<DatabaseBackupService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region Backup Operations

    /// <summary>
    /// Creates a new database backup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the created backup.</returns>
    public async Task<BackupResult> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Creating database backup");

        try
        {
            var backupInfo = await PostAsync<BackupInfo>($"{BaseEndpoint}/backup", cancellationToken: cancellationToken);

            var result = new BackupResult
            {
                Success = true,
                BackupInfo = backupInfo
            };

            _logger?.LogInformation("Created database backup {BackupId} ({Size})", 
                backupInfo.Id, backupInfo.SizeFormatted);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create database backup");
            return new BackupResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Retrieves a list of all available database backups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of backup information.</returns>
    public async Task<IEnumerable<BackupInfo>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting database backups list");

        try
        {
            var backups = await GetAsync<IEnumerable<BackupInfo>>($"{BaseEndpoint}/backups", cancellationToken: cancellationToken);

            _logger?.LogDebug("Retrieved {Count} database backups", backups?.Count() ?? 0);
            return backups ?? Enumerable.Empty<BackupInfo>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get database backups");
            throw;
        }
    }

    /// <summary>
    /// Gets information about a specific backup.
    /// </summary>
    /// <param name="backupId">The ID of the backup to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Backup information if found, null otherwise.</returns>
    public async Task<BackupInfo?> GetBackupInfoAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            throw new ArgumentException("Backup ID cannot be null or empty", nameof(backupId));

        try
        {
            var backups = await GetBackupsAsync(cancellationToken);
            return backups.FirstOrDefault(b => b.Id == backupId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get backup info for {BackupId}", backupId);
            return null;
        }
    }

    #endregion

    #region Restore Operations

    /// <summary>
    /// Restores the database from a specific backup.
    /// </summary>
    /// <param name="backupId">The ID of the backup to restore from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the restore operation.</returns>
    public async Task<RestoreResult> RestoreBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            throw new ArgumentException("Backup ID cannot be null or empty", nameof(backupId));

        _logger?.LogDebug("Restoring database from backup {BackupId}", backupId);

        try
        {
            // Use PostAsync without return type since restore typically returns success message
            await PostAsync($"{BaseEndpoint}/restore/{Uri.EscapeDataString(backupId)}", cancellationToken: cancellationToken);

            var result = new RestoreResult
            {
                Success = true
            };

            _logger?.LogInformation("Successfully restored database from backup {BackupId}", backupId);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore database from backup {BackupId}", backupId);
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region Download Operations

    /// <summary>
    /// Downloads a backup file as a byte array.
    /// </summary>
    /// <param name="backupId">The ID of the backup to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The backup file content as a byte array.</returns>
    public async Task<byte[]> DownloadBackupAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            throw new ArgumentException("Backup ID cannot be null or empty", nameof(backupId));

        _logger?.LogDebug("Downloading backup {BackupId}", backupId);

        try
        {
            var response = await HttpClient.GetAsync($"{BaseEndpoint}/download/{Uri.EscapeDataString(backupId)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger?.LogInformation("Downloaded backup {BackupId} ({Size} bytes)", backupId, content.Length);
            return content;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download backup {BackupId}", backupId);
            throw;
        }
    }

    /// <summary>
    /// Downloads a backup file and saves it to the specified path.
    /// </summary>
    /// <param name="backupId">The ID of the backup to download.</param>
    /// <param name="filePath">The local file path to save the backup to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DownloadBackupToFileAsync(string backupId, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            throw new ArgumentException("Backup ID cannot be null or empty", nameof(backupId));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        _logger?.LogDebug("Downloading backup {BackupId} to {FilePath}", backupId, filePath);

        try
        {
            var content = await DownloadBackupAsync(backupId, cancellationToken);
            await File.WriteAllBytesAsync(filePath, content, cancellationToken);

            _logger?.LogInformation("Downloaded backup {BackupId} to {FilePath}", backupId, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download backup {BackupId} to {FilePath}", backupId, filePath);
            throw;
        }
    }

    /// <summary>
    /// Downloads a backup file as a stream.
    /// </summary>
    /// <param name="backupId">The ID of the backup to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the backup file content.</returns>
    public async Task<Stream> DownloadBackupAsStreamAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            throw new ArgumentException("Backup ID cannot be null or empty", nameof(backupId));

        _logger?.LogDebug("Downloading backup {BackupId} as stream", backupId);

        try
        {
            var response = await HttpClient.GetAsync($"{BaseEndpoint}/download/{Uri.EscapeDataString(backupId)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            _logger?.LogDebug("Started streaming backup {BackupId}", backupId);
            return stream;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download backup {BackupId} as stream", backupId);
            throw;
        }
    }

    #endregion

    #region Utility Operations

    /// <summary>
    /// Checks if a backup with the specified ID exists.
    /// </summary>
    /// <param name="backupId">The ID of the backup to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the backup exists, false otherwise.</returns>
    public async Task<bool> BackupExistsAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            return false;

        try
        {
            var backup = await GetBackupInfoAsync(backupId, cancellationToken);
            return backup != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the most recent backup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent backup, or null if no backups exist.</returns>
    public async Task<BackupInfo?> GetMostRecentBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var backups = await GetBackupsAsync(cancellationToken);
            return backups.OrderByDescending(b => b.CreatedAt).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get most recent backup");
            return null;
        }
    }

    /// <summary>
    /// Gets backups created within a specific date range.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Backups created within the specified date range.</returns>
    public async Task<IEnumerable<BackupInfo>> GetBackupsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date cannot be greater than end date");

        try
        {
            var backups = await GetBackupsAsync(cancellationToken);
            return backups.Where(b => b.CreatedAt >= startDate && b.CreatedAt <= endDate)
                         .OrderByDescending(b => b.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get backups by date range");
            throw;
        }
    }

    /// <summary>
    /// Gets backup storage statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Storage statistics for all backups.</returns>
    public async Task<object> GetBackupStorageStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var backups = await GetBackupsAsync(cancellationToken);
            var backupsList = backups.ToList();

            var totalSize = backupsList.Sum(b => b.SizeBytes);
            var averageSize = backupsList.Any() ? totalSize / backupsList.Count : 0;

            return new
            {
                TotalBackups = backupsList.Count,
                TotalSizeBytes = totalSize,
                TotalSizeFormatted = FormatBytes(totalSize),
                AverageSizeBytes = averageSize,
                AverageSizeFormatted = FormatBytes(averageSize),
                OldestBackup = backupsList.OrderBy(b => b.CreatedAt).FirstOrDefault()?.CreatedAt,
                NewestBackup = backupsList.OrderByDescending(b => b.CreatedAt).FirstOrDefault()?.CreatedAt,
                LargestBackup = backupsList.OrderByDescending(b => b.SizeBytes).FirstOrDefault()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get backup storage statistics");
            throw;
        }
    }

    /// <summary>
    /// Creates a backup and immediately downloads it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The backup file content as a byte array.</returns>
    public async Task<byte[]> CreateAndDownloadBackupAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Creating and downloading backup");

        try
        {
            var backupResult = await CreateBackupAsync(cancellationToken);
            if (!backupResult.Success || backupResult.BackupInfo == null)
            {
                throw new InvalidOperationException($"Failed to create backup: {backupResult.ErrorMessage}");
            }

            var content = await DownloadBackupAsync(backupResult.BackupInfo.Id, cancellationToken);

            _logger?.LogInformation("Created and downloaded backup {BackupId}", backupResult.BackupInfo.Id);
            return content;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create and download backup");
            throw;
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Formats bytes into a human-readable string.
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    /// <returns>A formatted string representation of the size.</returns>
    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    #endregion
}