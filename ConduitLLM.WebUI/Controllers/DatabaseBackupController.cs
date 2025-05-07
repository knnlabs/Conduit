using System;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Controller for database backup and restore operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyOnly")]
    public class DatabaseBackupController : ControllerBase
    {
        private readonly IDatabaseBackupService _backupService;
        private readonly ILogger<DatabaseBackupController> _logger;

        public DatabaseBackupController(
            IDatabaseBackupService backupService,
            ILogger<DatabaseBackupController> logger)
        {
            _backupService = backupService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a backup of the database and returns it as a downloadable file
        /// </summary>
        /// <returns>The database backup file</returns>
        [HttpGet("backup")]
        public async Task<IActionResult> BackupDatabase()
        {
            try
            {
                var backupData = await _backupService.CreateBackupAsync();
                string provider = _backupService.GetDatabaseProvider();
                
                string contentType;
                string fileName;
                
                if (provider == "sqlite")
                {
                    contentType = "application/x-sqlite3";
                    fileName = $"conduit_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
                }
                else // postgres or other providers
                {
                    contentType = "application/json";
                    fileName = $"conduit_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                }
                
                return File(backupData, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database backup");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to create database backup", error = ex.Message });
            }
        }

        /// <summary>
        /// Restores the database from a backup file
        /// </summary>
        /// <param name="file">The backup file</param>
        /// <returns>Result of the restore operation</returns>
        [HttpPost("restore")]
        public async Task<IActionResult> RestoreDatabase(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                byte[] backupData = memoryStream.ToArray();
                
                // Validate backup
                if (!await _backupService.ValidateBackupAsync(backupData))
                {
                    return BadRequest(new { message = "Invalid backup file format" });
                }
                
                // Perform restore
                bool success = await _backupService.RestoreFromBackupAsync(backupData);
                
                if (success)
                {
                    return Ok(new { message = "Database restored successfully" });
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to restore database" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring database from backup");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to restore database", error = ex.Message });
            }
        }
    }
}