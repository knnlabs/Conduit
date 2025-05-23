using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for IAdminApiClient
    /// </summary>
    public static class AdminApiClientExtensions
    {
        /// <summary>
        /// Generates a virtual key (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="request">The create request</param>
        /// <returns>The created key response</returns>
        /// <exception cref="ArgumentNullException">Thrown when client or request is null</exception>
        public static async Task<CreateVirtualKeyResponseDto?> GenerateVirtualKeyAsync(
            this IAdminApiClient client, 
            CreateVirtualKeyRequestDto request)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            
            // Validate request properties
            if (string.IsNullOrWhiteSpace(request.KeyName))
            {
                throw new ArgumentException("Key name is required", nameof(request));
            }
            
            // Simply delegates to the CreateVirtualKeyAsync method
            return await client.CreateVirtualKeyAsync(request);
        }
        
        /// <summary>
        /// Updates the spend amount for a virtual key (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="virtualKeyId">The ID of the virtual key</param>
        /// <param name="cost">The cost to add to the current spend</param>
        /// <returns>True if successful, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when client is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when virtualKeyId is less than or equal to zero</exception>
        public static async Task<bool> UpdateVirtualKeySpendAsync(
            this IAdminApiClient client,
            int virtualKeyId,
            decimal cost)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            
            if (virtualKeyId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(virtualKeyId), "Virtual key ID must be greater than zero");
            }
            
            // No need to create an UpdateSpendRequest as the API method directly accepts cost parameter
            return await client.UpdateVirtualKeySpendAsync(virtualKeyId, cost);
        }
        
        /// <summary>
        /// Gets a virtual key (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="id">The virtual key ID</param>
        /// <returns>The virtual key DTO</returns>
        public static async Task<VirtualKeyDto?> GetVirtualKeyAsync(
            this IAdminApiClient client,
            int id)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Virtual key ID must be greater than zero");
            }
            
            // Delegates to the proper method
            return await client.GetVirtualKeyByIdAsync(id);
        }
        
        /// <summary>
        /// Backs up the database (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="backupPath">The path to save the backup</param>
        /// <returns>True if successful, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when client is null</exception>
        public static async Task<bool> BackupDatabaseAsync(
            this IAdminApiClient client,
            string backupPath)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            
            // backupPath is ignored in the new API design, but we still validate it for consistency
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                // Log a warning about this, but don't throw since it's a compatibility method
                System.Diagnostics.Debug.WriteLine($"Warning: backupPath parameter is ignored in the Admin API implementation. The backup will be created using the server's configured backup location.");
            }
            
            // Delegates to the proper method
            return await client.CreateDatabaseBackupAsync();
        }
        
        /// <summary>
        /// Restores the database from a backup (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="backupPath">The path to the backup file</param>
        /// <returns>True if successful, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when client is null</exception>
        /// <remarks>
        /// This method is not directly supported through the Admin API and always returns false.
        /// In the Admin API design, database restoration is handled via direct API endpoints 
        /// rather than file paths.
        /// </remarks>
        public static async Task<bool> RestoreDatabaseAsync(
            this IAdminApiClient client,
            string backupPath)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            
            // backupPath is ignored in the new API design, but we still validate it for consistency
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                // Log a warning about this, but don't throw since it's a compatibility method
                System.Diagnostics.Debug.WriteLine($"Warning: RestoreDatabaseAsync is not supported in the Admin API implementation. Use the appropriate Admin API endpoint instead.");
            }
            
            // Not directly supported through the Admin API, return false
            await Task.CompletedTask; // Just to make it asynchronous for compatibility
            return false;
        }
        
        /// <summary>
        /// Gets available database backups (backward compatibility method)
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <returns>A list of available backups</returns>
        /// <exception cref="ArgumentNullException">Thrown when client is null</exception>
        /// <remarks>
        /// This method is not directly supported through the Admin API and always returns an empty list.
        /// In the Admin API design, database backup listing is handled via direct API endpoints.
        /// </remarks>
        public static async Task<List<string>> GetAvailableDatabaseBackupsAsync(
            this IAdminApiClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            
            // Log a warning about this, but don't throw since it's a compatibility method
            System.Diagnostics.Debug.WriteLine($"Warning: GetAvailableDatabaseBackupsAsync is not supported in the Admin API implementation. Use the appropriate Admin API endpoint instead.");
            
            // Not directly supported through the Admin API, return empty list
            await Task.CompletedTask; // Just to make it asynchronous for compatibility
            return new List<string>();
        }
    }
}