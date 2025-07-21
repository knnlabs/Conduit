namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents information about a database backup.
/// </summary>
public class BackupInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the backup.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name of the backup.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the backup was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the size of the backup file in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the human-readable formatted size of the backup file.
    /// </summary>
    public string SizeFormatted { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of a backup operation.
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Gets or sets whether the backup operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the backup operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the backup information if the operation was successful.
    /// </summary>
    public BackupInfo? BackupInfo { get; set; }
}

/// <summary>
/// Represents the result of a restore operation.
/// </summary>
public class RestoreResult
{
    /// <summary>
    /// Gets or sets whether the restore operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the restore operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional details about the restore operation.
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Options for backup operations.
/// </summary>
public class BackupOptions
{
    /// <summary>
    /// Gets or sets whether to include schema in the backup.
    /// </summary>
    public bool IncludeSchema { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include data in the backup.
    /// </summary>
    public bool IncludeData { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to compress the backup file.
    /// </summary>
    public bool Compress { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom description for the backup.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets tags to associate with the backup.
    /// </summary>
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Options for restore operations.
/// </summary>
public class RestoreOptions
{
    /// <summary>
    /// Gets or sets whether to overwrite existing data during restore.
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to verify the backup before restoring.
    /// </summary>
    public bool VerifyBeforeRestore { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to create a backup before restoring.
    /// </summary>
    public bool BackupBeforeRestore { get; set; } = true;

    /// <summary>
    /// Gets or sets specific tables to restore (empty list means all tables).
    /// </summary>
    public List<string>? SpecificTables { get; set; }
}

/// <summary>
/// Represents backup validation results.
/// </summary>
public class BackupValidationResult
{
    /// <summary>
    /// Gets or sets whether the backup is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets validation errors if any.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets validation warnings if any.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets metadata about the backup contents.
    /// </summary>
    public BackupMetadata? Metadata { get; set; }
}

/// <summary>
/// Represents metadata about backup contents.
/// </summary>
public class BackupMetadata
{
    /// <summary>
    /// Gets or sets the database type (SQLite, PostgreSQL, etc.).
    /// </summary>
    public string DatabaseType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database version.
    /// </summary>
    public string DatabaseVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backup format version.
    /// </summary>
    public string BackupFormatVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of tables included in the backup.
    /// </summary>
    public List<string> Tables { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of records in the backup.
    /// </summary>
    public long TotalRecords { get; set; }

    /// <summary>
    /// Gets or sets when the backup was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents backup storage statistics.
/// </summary>
public class BackupStorageStats
{
    /// <summary>
    /// Gets or sets the total number of backups.
    /// </summary>
    public int TotalBackups { get; set; }

    /// <summary>
    /// Gets or sets the total storage size in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the formatted total storage size.
    /// </summary>
    public string TotalSizeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the average backup size in bytes.
    /// </summary>
    public long AverageSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the formatted average backup size.
    /// </summary>
    public string AverageSizeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date of the oldest backup.
    /// </summary>
    public DateTime? OldestBackup { get; set; }

    /// <summary>
    /// Gets or sets the date of the newest backup.
    /// </summary>
    public DateTime? NewestBackup { get; set; }

    /// <summary>
    /// Gets or sets information about the largest backup.
    /// </summary>
    public BackupInfo? LargestBackup { get; set; }
}