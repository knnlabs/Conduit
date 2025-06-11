using System;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Utility for managing database migrations.
    /// </summary>
    public static class DatabaseMigrationUtility
    {
        /// <summary>
        /// Applies any pending migrations to the database.
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <param name="logger">Optional logger for migration messages</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider, ILogger? logger = null)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

                logger?.LogInformation("Checking for pending database migrations...");

                // Apply migrations if needed
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    logger?.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Count());
                    await dbContext.Database.MigrateAsync();
                    logger?.LogInformation("Database migrations applied successfully.");
                }
                else
                {
                    logger?.LogInformation("No pending migrations found.");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error applying database migrations.");
                throw;
            }
        }

        /// <summary>
        /// Updates the database schema to ensure the MaxContextTokens column exists on the ModelProviderMapping table.
        /// This is a fallback for when proper migrations cannot be applied.
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <param name="logger">Optional logger for migration messages</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task EnsureMaxContextTokensColumnExistsAsync(IServiceProvider serviceProvider, ILogger? logger = null)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

                logger?.LogInformation("Checking for MaxContextTokens column...");

                // Check if the column exists using a raw SQL query
                var columnExists = false;

                try
                {
                    // This query checks if the column exists in the SQLite schema
                    var query = @"
                        SELECT COUNT(*) 
                        FROM pragma_table_info('ModelProviderMappings') 
                        WHERE name = 'MaxContextTokens'";

                    var result = await dbContext.Database.ExecuteSqlRawAsync(query);
                    columnExists = result > 0;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Error checking for MaxContextTokens column existence.");
                }

                if (!columnExists)
                {
                    logger?.LogInformation("Adding MaxContextTokens column to ModelProviderMappings table...");

                    try
                    {
                        // Add the column using a raw SQL query (SQLite syntax)
                        var alterTableQuery = @"
                            ALTER TABLE ModelProviderMappings 
                            ADD COLUMN MaxContextTokens INTEGER NULL";

                        await dbContext.Database.ExecuteSqlRawAsync(alterTableQuery);
                        logger?.LogInformation("MaxContextTokens column added successfully.");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error adding MaxContextTokens column.");
                        // Don't throw here, let the application continue without the column
                    }
                }
                else
                {
                    logger?.LogInformation("MaxContextTokens column already exists.");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error ensuring MaxContextTokens column exists.");
                // Don't throw, let the application continue
            }
        }
    }
}
