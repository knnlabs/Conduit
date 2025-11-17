using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Extension methods for database migration
    /// </summary>
    public static class MigrationExtensions
    {
        /// <summary>
        /// Add migration services to DI container
        /// </summary>
        public static IServiceCollection AddDatabaseMigration(this IServiceCollection services)
        {
            services.AddScoped<SimpleMigrationService>();
            return services;
        }

        /// <summary>
        /// Run database migrations during startup
        /// </summary>
        public static async Task RunDatabaseMigrationAsync(this IHost app)
        {
            var skipDatabaseInit = Environment.GetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT")?.ToUpperInvariant() == "TRUE";
            
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SimpleMigrationService>>();

            if (skipDatabaseInit)
            {
                logger.LogWarning("CONDUIT_SKIP_DATABASE_INIT is set. Skipping database migrations.");
                logger.LogWarning("Ensure your database schema is up to date!");
                return;
            }

            var migrationService = scope.ServiceProvider.GetRequiredService<SimpleMigrationService>();
            
            try
            {
                logger.LogInformation("Running database migrations...");
                
                var success = await migrationService.MigrateAsync();
                
                if (!success)
                {
                    throw new InvalidOperationException("Database migration failed. Check logs for details.");
                }
                
                logger.LogInformation("Database migrations completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run database migrations");
                throw new InvalidOperationException("Database migration failed. Application cannot start.", ex);
            }
        }
    }
}