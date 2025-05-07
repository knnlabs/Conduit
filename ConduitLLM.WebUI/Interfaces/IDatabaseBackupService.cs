using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service interface for database backup and restore operations
    /// </summary>
    public interface IDatabaseBackupService
    {
        /// <summary>
        /// Creates a backup of the current database
        /// </summary>
        /// <returns>A byte array containing the database backup</returns>
        Task<byte[]> CreateBackupAsync();

        /// <summary>
        /// Restores the database from a backup
        /// </summary>
        /// <param name="backupData">The backup data as a byte array</param>
        /// <returns>A boolean indicating whether the restore was successful</returns>
        Task<bool> RestoreFromBackupAsync(byte[] backupData);
        
        /// <summary>
        /// Validates if the provided data is a valid database backup
        /// </summary>
        /// <param name="backupData">The backup data to validate</param>
        /// <returns>True if the data represents a valid backup, false otherwise</returns>
        Task<bool> ValidateBackupAsync(byte[] backupData);
        
        /// <summary>
        /// Gets the current database provider
        /// </summary>
        /// <returns>The database provider name (sqlite or postgres)</returns>
        string GetDatabaseProvider();
    }
}