using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Hosting;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for database backup functionality through the Admin API
/// </summary>
public partial class AdminDatabaseBackupService : IAdminDatabaseBackupService
{
    private readonly IConfigurationDbContext _dbContext;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly ILogger<AdminDatabaseBackupService> _logger;
    private readonly string _backupDirectory;

    /// <summary>
    /// Initializes a new instance of the AdminDatabaseBackupService class
    /// </summary>
    /// <param name="dbContext">The database context</param>
    /// <param name="hostingEnvironment">The hosting environment</param>
    /// <param name="logger">The logger</param>
    public AdminDatabaseBackupService(
        IConfigurationDbContext dbContext,
        IWebHostEnvironment hostingEnvironment,
        ILogger<AdminDatabaseBackupService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create backup directory if it doesn't exist
        _backupDirectory = Path.Combine(_hostingEnvironment.ContentRootPath, "backups");
        if (!Directory.Exists(_backupDirectory))
        {
            Directory.CreateDirectory(_backupDirectory);
        }
    }

    /// <inheritdoc/>
    public async Task<BackupResult> CreateBackupAsync()
    {
        try
        {
            _logger.LogInformation("Creating database backup");

            // Get database provider
            var providerName = _dbContext.GetDatabase().ProviderName;

            if (providerName?.Contains("Npgsql") == true)
            {
                return await CreatePostgresBackupAsync();
            }
            else
            {
                var errorMessage = $"Only PostgreSQL is supported. Invalid provider: {providerName}";
                _logger.LogError("{ErrorMessage}", errorMessage.Replace(Environment.NewLine, ""));
                return new BackupResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database backup");
            return new BackupResult
            {
                Success = false,
                ErrorMessage = $"An unexpected error occurred: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<List<BackupInfo>> GetBackupsAsync()
    {
        try
        {
            _logger.LogInformation("Getting list of database backups");

            var backupFiles = Directory.GetFiles(_backupDirectory, "*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            var backups = backupFiles.Select(f => new BackupInfo
            {
                Id = Path.GetFileNameWithoutExtension(f.Name),
                FileName = f.Name,
                CreatedAt = f.LastWriteTime,
                SizeBytes = f.Length,
                SizeFormatted = FormatFileSize(f.Length)
            }).ToList();

            return await Task.FromResult(backups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting list of database backups");
            return new List<BackupInfo>();
        }
    }

    /// <inheritdoc/>
    public async Task<RestoreResult> RestoreBackupAsync(string backupId)
    {
        try
        {
_logger.LogInformation("Restoring database backup: {BackupId}", backupId.Replace(Environment.NewLine, ""));

            // Validate backup ID to prevent path traversal
            if (!IsValidBackupId(backupId))
            {
                var errorMessage = "Invalid backup ID format";
_logger.LogError("Invalid backup ID format: {BackupId}", backupId.Replace(Environment.NewLine, ""));
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            // Find the backup file
            var backupFile = Directory.GetFiles(_backupDirectory, "*.zip")
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == backupId);

            if (backupFile == null)
            {
                var errorMessage = $"Backup file not found: {backupId}";
                _logger.LogError("{ErrorMessage}", errorMessage.Replace(Environment.NewLine, ""));
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            // Get database provider
            var providerName = _dbContext.GetDatabase().ProviderName;

            if (providerName?.Contains("Sqlite") == true)
            {
                return await RestoreSqliteBackupAsync(backupFile);
            }
            else if (providerName?.Contains("Npgsql") == true)
            {
                return await RestorePostgresBackupAsync(backupFile);
            }
            else
            {
                var errorMessage = $"Unsupported database provider: {providerName}";
                _logger.LogError("{ErrorMessage}", errorMessage.Replace(Environment.NewLine, ""));
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error restoring database backup: {BackupId}", backupId.Replace(Environment.NewLine, ""));
            return new RestoreResult
            {
                Success = false,
                ErrorMessage = $"An unexpected error occurred: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public Task<(Stream FileStream, string ContentType, string FileName)?> DownloadBackupAsync(string backupId)
    {
        try
        {
_logger.LogInformation("Downloading database backup: {BackupId}", backupId.Replace(Environment.NewLine, ""));

            // Validate backup ID to prevent path traversal
            if (!IsValidBackupId(backupId))
            {
_logger.LogError("Invalid backup ID format for download: {BackupId}", backupId.Replace(Environment.NewLine, ""));
                return Task.FromResult<(Stream FileStream, string ContentType, string FileName)?>(null);
            }

            // Find the backup file
            var backupFile = Directory.GetFiles(_backupDirectory, "*.zip")
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == backupId);

            if (backupFile == null)
            {
_logger.LogError("Backup file not found: {BackupId}", backupId.Replace(Environment.NewLine, ""));
                return Task.FromResult<(Stream FileStream, string ContentType, string FileName)?>(null);
            }

            // Create file stream
            var fileStream = new FileStream(backupFile, FileMode.Open, FileAccess.Read);
            var fileName = Path.GetFileName(backupFile);

            // Create a tuple with the file stream, content type, and file name
            var result = ((Stream)fileStream, "application/zip", fileName);
            return Task.FromResult<(Stream FileStream, string ContentType, string FileName)?>(result);
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error downloading database backup: {BackupId}", backupId.Replace(Environment.NewLine, ""));
            return Task.FromResult<(Stream FileStream, string ContentType, string FileName)?>(null);
        }
    }
}
