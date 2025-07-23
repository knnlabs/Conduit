using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Proper database initialization for application startup
    /// </summary>
    public static class DatabaseStartup
    {
        /// <summary>
        /// Initialize database following EF Core best practices
        /// </summary>
        public static async Task<bool> InitializeDatabaseAsync(
            IServiceProvider serviceProvider,
            IHostEnvironment environment)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MigrationService>>();
            
            // Development environment strategy
            if (environment.IsDevelopment())
            {
                return await InitializeDevelopmentDatabaseAsync(serviceProvider, logger);
            }
            
            // Production environment strategy
            return await InitializeProductionDatabaseAsync(serviceProvider, logger);
        }

        private static async Task<bool> InitializeDevelopmentDatabaseAsync(
            IServiceProvider serviceProvider,
            ILogger logger)
        {
            logger.LogInformation("Initializing database for development environment");
            
            // In development, we have more flexibility
            var migrationService = serviceProvider.GetRequiredService<MigrationService>();
            
            // Check if we should reset the database
            var resetDatabase = Environment.GetEnvironmentVariable("RESET_DATABASE_ON_STARTUP")?.ToUpperInvariant() == "TRUE";
            
            if (resetDatabase)
            {
                logger.LogWarning("RESET_DATABASE_ON_STARTUP is enabled. Dropping and recreating database...");
                
                using var scope = serviceProvider.CreateScope();
                var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
                using var context = await contextFactory.CreateDbContextAsync();
                
                try
                {
                    await context.Database.EnsureDeletedAsync();
                    logger.LogInformation("Database dropped successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to drop database");
                }
            }
            
            // Apply migrations
            var result = await migrationService.ApplyMigrationsAsync();
            
            if (!result.Success)
            {
                logger.LogError("Database migration failed: {Error}", result.Error);
                
                // In development, offer to recreate if migrations fail
                if (result.RequiresManualIntervention)
                {
                    logger.LogError(
                        "Database requires manual intervention. " +
                        "Consider setting RESET_DATABASE_ON_STARTUP=TRUE to recreate the database");
                }
            }
            else
            {
                logger.LogInformation("Database initialized successfully");
                
                if (result.AppliedMigrations.Any())
                {
                    logger.LogInformation("Applied migrations: {Migrations}", 
                        string.Join(", ", result.AppliedMigrations));
                }
            }
            
            return result.Success;
        }

        private static async Task<bool> InitializeProductionDatabaseAsync(
            IServiceProvider serviceProvider,
            ILogger logger)
        {
            logger.LogInformation("Initializing database for production environment");
            
            var migrationService = serviceProvider.GetRequiredService<MigrationService>();
            
            // In production, we're more conservative
            var maxRetries = 5;
            var retryDelay = TimeSpan.FromSeconds(10);
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                logger.LogInformation("Database initialization attempt {Attempt}/{MaxRetries}", 
                    attempt, maxRetries);
                
                var result = await migrationService.ApplyMigrationsAsync();
                
                if (result.Success)
                {
                    logger.LogInformation("Database initialized successfully");
                    
                    if (result.WasAppliedByOtherInstance)
                    {
                        logger.LogInformation("Migrations were applied by another instance");
                    }
                    else if (result.AppliedMigrations.Any())
                    {
                        logger.LogInformation("Applied migrations: {Migrations}", 
                            string.Join(", ", result.AppliedMigrations));
                    }
                    
                    return true;
                }
                
                if (result.RequiresManualIntervention)
                {
                    logger.LogError(
                        "Database requires manual intervention: {Error}. " +
                        "This typically means the database was created with EnsureCreated " +
                        "or has migrations from a newer version of the application.",
                        result.Error);
                    
                    // Don't retry if manual intervention is required
                    return false;
                }
                
                logger.LogWarning(
                    "Database initialization attempt {Attempt} failed: {Error}", 
                    attempt, result.Error);
                
                if (attempt < maxRetries)
                {
                    logger.LogInformation("Waiting {Delay} before retry...", retryDelay);
                    await Task.Delay(retryDelay);
                }
            }
            
            logger.LogError("Database initialization failed after {MaxRetries} attempts", maxRetries);
            return false;
        }
        
        /// <summary>
        /// Register database services
        /// </summary>
        public static IServiceCollection AddDatabaseServices(
            this IServiceCollection services,
            string connectionString)
        {
            // Register the migration service
            services.AddScoped<MigrationService>();
            
            // Register DbContextFactory
            services.AddDbContextFactory<ConfigurationDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                    
                    // Use the same command timeout as migrations
                    npgsqlOptions.CommandTimeout(300); // 5 minutes
                });
                
                // In development, add more logging
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (environment == "Development")
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });
            
            return services;
        }
    }
}