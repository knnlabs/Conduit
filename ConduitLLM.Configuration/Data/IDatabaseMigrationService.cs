using System;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Service interface for managing database migrations across different database providers
    /// </summary>
    public interface IDatabaseMigrationService
    {
        /// <summary>
        /// Applies pending migrations to the database and ensures all required tables exist
        /// </summary>
        /// <param name="maxRetries">Maximum number of retries for database connection</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds</param>
        /// <returns>True if migrations were successfully applied, false otherwise</returns>
        Task<bool> ApplyMigrationsAsync(int maxRetries = 5, int retryDelayMs = 1000);

        /// <summary>
        /// Ensures that specific tables exist by creating them directly if necessary
        /// </summary>
        /// <param name="tableNames">Array of table names to ensure exist</param>
        /// <returns>True if all tables exist or were created successfully, false otherwise</returns>
        Task<bool> EnsureTablesExistAsync(params string[] tableNames);

        /// <summary>
        /// Gets the current database provider type
        /// </summary>
        /// <returns>The database provider type as a string ("sqlite" or "postgres")</returns>
        string GetDatabaseProviderType();
    }
}
