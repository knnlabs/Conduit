using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Extension methods for database initialization
    /// </summary>
    public static class DatabaseInitializationExtensions
    {
        /// <summary>
        /// Initializes the database by applying migrations and ensuring all required tables exist
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <param name="maxRetries">Maximum number of retries for database connection</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds</param>
        /// <returns>True if the database was initialized successfully, false otherwise</returns>
        public static async Task<bool> InitializeDatabaseAsync(
            this IServiceProvider serviceProvider,
            int maxRetries = 5,
            int retryDelayMs = 1000)
        {
            using var scope = serviceProvider.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseInitializer>>();

            logger.LogInformation("Starting database initialization");
            bool success = await initializer.InitializeDatabaseAsync(maxRetries, retryDelayMs);

            if (success)
            {
                logger.LogInformation("Database initialization completed successfully");
            }
            else
            {
                logger.LogWarning("Database initialization completed with warnings");
            }

            return success;
        }

        /// <summary>
        /// Ensures that specified tables exist in the database
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <param name="tableNames">Array of table names to ensure exist</param>
        /// <returns>True if all tables exist or were created successfully, false otherwise</returns>
        public static async Task<bool> EnsureTablesExistAsync(
            this IServiceProvider serviceProvider,
            params string[] tableNames)
        {
            using var scope = serviceProvider.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            return await initializer.EnsureTablesExistAsync(tableNames);
        }
    }
}
