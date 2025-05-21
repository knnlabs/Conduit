using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Tests.Services.Stubs
{
    /// <summary>
    /// Stub implementation of DatabaseBackupServiceAdapter for tests
    /// </summary>
    public class StubDatabaseBackupServiceAdapter : IDatabaseBackupService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<StubDatabaseBackupServiceAdapter> _logger;
        
        /// <summary>
        /// Initializes a new instance of the StubDatabaseBackupServiceAdapter class
        /// </summary>
        /// <param name="adminApiClient">The Admin API client</param>
        /// <param name="logger">The logger</param>
        public StubDatabaseBackupServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<StubDatabaseBackupServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public Task<byte[]> CreateBackupAsync()
        {
            return Task.FromResult(new byte[0]);
        }
        
        /// <inheritdoc />
        public Task<bool> RestoreFromBackupAsync(byte[] backupData)
        {
            return Task.FromResult(false);
        }
        
        /// <inheritdoc />
        public Task<bool> ValidateBackupAsync(byte[] backupData)
        {
            return Task.FromResult(backupData.Length > 0);
        }
        
        /// <inheritdoc />
        public string GetDatabaseProvider()
        {
            return "sqlite";
        }
        
        /// <summary>
        /// Backup database to a file path (for tests)
        /// </summary>
        public async Task<bool> BackupDatabaseAsync(string backupPath)
        {
            try
            {
                // Use the API's direct method
                return await _adminApiClient.CreateDatabaseBackupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up database");
                return false;
            }
        }
        
        /// <summary>
        /// Restore database from a file path (for tests)
        /// </summary>
        public virtual async Task<bool> RestoreDatabaseAsync(string backupPath)
        {
            try
            {
                // Add await to make this properly async
                await Task.CompletedTask;
                
                // Stub implementation 
                return false; // Currently not implemented in the Admin API
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring database");
                return false;
            }
        }
        
        /// <summary>
        /// Get available database backups (for tests)
        /// </summary>
        public async Task<List<string>> GetAvailableBackupsAsync()
        {
            try
            {
                // Add await to make this properly async
                await Task.CompletedTask;
                
                // Return empty list as this functionality is not directly exposed by the Admin API
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available backups");
                return new List<string>();
            }
        }
    }
}