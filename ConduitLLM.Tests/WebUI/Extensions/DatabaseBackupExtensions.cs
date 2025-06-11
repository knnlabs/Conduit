using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for the IAdminApiClient for database backup operations in tests
    /// </summary>
    public static class DatabaseBackupExtensions
    {
        /// <summary>
        /// Gets available database backups from the admin API
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <returns>List of available backup file names</returns>
        public static Task<List<string>> GetAvailableDatabaseBackupsAsync(this IAdminApiClient client)
        {
            // For test mocking, return some test backup filenames
            return Task.FromResult(new List<string>
            {
                "backup_20250101_120000.bak",
                "backup_20250102_120000.bak",
                "backup_20250103_120000.bak"
            });
        }

        /// <summary>
        /// Restores the database from a backup file
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="backupFileName">Name of the backup file to restore</param>
        /// <returns>True if restore was successful</returns>
        public static Task<bool> RestoreDatabaseAsync(this IAdminApiClient client, string backupFileName)
        {
            // For test mocking, return success
            return Task.FromResult(true);
        }

        /// <summary>
        /// Restores the database backup
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="backupFileName">The backup file name</param>
        /// <returns>True if the operation was successful</returns>
        public static Task<bool> RestoreDatabaseBackupAsync(this IAdminApiClient client, string backupFileName)
        {
            // Alias for RestoreDatabaseAsync
            return client.RestoreDatabaseAsync(backupFileName);
        }
    }
}
