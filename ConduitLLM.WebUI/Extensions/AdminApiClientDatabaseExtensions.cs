using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for IAdminApiClient for database backup operations
    /// </summary>
    public static class AdminApiClientDatabaseExtensions
    {
        /// <summary>
        /// Gets available database backups (compatibility method)
        /// </summary>
        /// <param name="client">The API client</param>
        /// <returns>List of backup filenames</returns>
        public static async Task<List<string>> GetAvailableDatabaseBackupsAsync(this IAdminApiClient client)
        {
            if (client == null)
            {
                throw new System.ArgumentNullException(nameof(client));
            }
            
            // There's no direct method for this in the new API, so we return an empty list
            // The UI should handle this appropriately
            await Task.CompletedTask; // This ensures the method is actually async
            return new List<string>();
        }

        /// <summary>
        /// Restores a database from backup (compatibility method)
        /// </summary>
        /// <param name="client">The API client</param>
        /// <param name="backupName">Backup file name to restore</param>
        /// <returns>True if successful</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when client is null</exception>
        public static async Task<bool> RestoreDatabaseAsync(this IAdminApiClient client, string backupName)
        {
            if (client == null)
            {
                throw new System.ArgumentNullException(nameof(client));
            }
            
            // Log warning about using deprecated functionality
            System.Diagnostics.Debug.WriteLine($"Warning: RestoreDatabaseAsync is not supported in the Admin API implementation.");
            
            // The new API doesn't have a direct restore method
            // This is a stub for backward compatibility
            await Task.CompletedTask; // This ensures the method is actually async
            return false;
        }
        
        /// <summary>
        /// Backs up the database to a specified path (compatibility method)
        /// </summary>
        /// <param name="client">The API client</param>
        /// <param name="backupPath">The path to save the backup</param>
        /// <returns>True if successful, false otherwise</returns>
        public static async Task<bool> BackupDatabaseAsync(this IAdminApiClient client, string backupPath)
        {
            // In the current implementation, this is a stub method as the Admin API does not directly
            // expose this functionality in the same way as the legacy code.
            return await client.CreateDatabaseBackupAsync();
        }
    }
}