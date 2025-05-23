namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for database backup functionality through the Admin API
/// </summary>
public interface IAdminDatabaseBackupService
{
    /// <summary>
    /// Creates a database backup
    /// </summary>
    /// <returns>Backup info with success status and backup file path or error message</returns>
    Task<BackupResult> CreateBackupAsync();
    
    /// <summary>
    /// Gets a list of available backups
    /// </summary>
    /// <returns>List of backup info</returns>
    Task<List<BackupInfo>> GetBackupsAsync();
    
    /// <summary>
    /// Restores a database backup
    /// </summary>
    /// <param name="backupId">The ID or filename of the backup to restore</param>
    /// <returns>Restore result with success status and error message if failed</returns>
    Task<RestoreResult> RestoreBackupAsync(string backupId);
    
    /// <summary>
    /// Downloads a database backup
    /// </summary>
    /// <param name="backupId">The ID or filename of the backup to download</param>
    /// <returns>The backup file stream and content type</returns>
    Task<(Stream FileStream, string ContentType, string FileName)?> DownloadBackupAsync(string backupId);
}

/// <summary>
/// Result of a backup operation
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Whether the backup was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if the backup failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Backup information if successful
    /// </summary>
    public BackupInfo? BackupInfo { get; set; }
}

/// <summary>
/// Result of a restore operation
/// </summary>
public class RestoreResult
{
    /// <summary>
    /// Whether the restore was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if the restore failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about a database backup
/// </summary>
public class BackupInfo
{
    /// <summary>
    /// Unique identifier for the backup
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// File name of the backup
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Date and time when the backup was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Size of the backup file in bytes
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// Human-readable size of the backup file
    /// </summary>
    public string SizeFormatted { get; set; } = string.Empty;
}