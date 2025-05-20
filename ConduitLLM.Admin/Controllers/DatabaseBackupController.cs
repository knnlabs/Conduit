using ConduitLLM.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for database backup operations
/// </summary>
[ApiController]
[Route("api/database")]
[Authorize(Policy = "MasterKeyPolicy")]
public class DatabaseBackupController : ControllerBase
{
    private readonly IAdminDatabaseBackupService _databaseBackupService;
    private readonly ILogger<DatabaseBackupController> _logger;
    
    /// <summary>
    /// Initializes a new instance of the DatabaseBackupController
    /// </summary>
    /// <param name="databaseBackupService">The database backup service</param>
    /// <param name="logger">The logger</param>
    public DatabaseBackupController(
        IAdminDatabaseBackupService databaseBackupService,
        ILogger<DatabaseBackupController> logger)
    {
        _databaseBackupService = databaseBackupService ?? throw new ArgumentNullException(nameof(databaseBackupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Creates a database backup
    /// </summary>
    /// <returns>The created backup information</returns>
    [HttpPost("backup")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(BackupInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBackup()
    {
        try
        {
            var result = await _databaseBackupService.CreateBackupAsync();
            
            if (!result.Success)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
            }
            
            return Ok(result.BackupInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database backup");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
    
    /// <summary>
    /// Gets a list of available backups
    /// </summary>
    /// <returns>List of backup information</returns>
    [HttpGet("backups")]
    [ProducesResponseType(typeof(List<BackupInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBackups()
    {
        try
        {
            var backups = await _databaseBackupService.GetBackupsAsync();
            return Ok(backups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database backups");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
    
    /// <summary>
    /// Restores a database backup
    /// </summary>
    /// <param name="backupId">The ID of the backup to restore</param>
    /// <returns>Success message or error</returns>
    [HttpPost("restore/{backupId}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RestoreBackup(string backupId)
    {
        try
        {
            var result = await _databaseBackupService.RestoreBackupAsync(backupId);
            
            if (!result.Success)
            {
                if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(result.ErrorMessage);
                }
                
                return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
            }
            
            return Ok("Database restored successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring database backup");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
    
    /// <summary>
    /// Downloads a database backup
    /// </summary>
    /// <param name="backupId">The ID of the backup to download</param>
    /// <returns>The backup file</returns>
    [HttpGet("download/{backupId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadBackup(string backupId)
    {
        try
        {
            var result = await _databaseBackupService.DownloadBackupAsync(backupId);
            
            if (result == null)
            {
                return NotFound($"Backup with ID {backupId} not found");
            }
            
            var (fileStream, contentType, fileName) = result.Value;
            
            return File(fileStream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading database backup");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}