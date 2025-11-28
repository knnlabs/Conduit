using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;

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

        /// <summary>
        /// Seed default data after migrations complete.
        /// This ensures essential configuration exists.
        /// </summary>
        public static async Task SeedDefaultDataAsync(this IHost app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SimpleMigrationService>>();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            try
            {
                await SeedDefaultRetentionPolicyAsync(context, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding default data - application will continue");
                // Don't throw - seeding is not critical for startup
            }
        }

        private static async Task SeedDefaultRetentionPolicyAsync(
            ConduitDbContext context,
            ILogger logger)
        {
            // Check if any default policy exists
            var hasDefault = await context.MediaRetentionPolicies
                .AnyAsync(p => p.IsDefault);

            if (hasDefault)
            {
                logger.LogDebug("Default media retention policy already exists");
                return;
            }

            // Check if any policies exist at all
            var hasAnyPolicy = await context.MediaRetentionPolicies.AnyAsync();

            if (hasAnyPolicy)
            {
                logger.LogInformation(
                    "Media retention policies exist but none is marked as default. " +
                    "Consider setting a default policy via the Admin API.");
                return;
            }

            // Create default policy with reasonable settings
            var defaultPolicy = new MediaRetentionPolicy
            {
                Name = "Default",
                Description = "System default retention policy. Media retention varies by account balance: " +
                             "60 days for positive balance, 14 days for zero balance, 3 days for negative balance.",
                PositiveBalanceRetentionDays = 60,
                ZeroBalanceRetentionDays = 14,
                NegativeBalanceRetentionDays = 3,
                SoftDeleteGracePeriodDays = 7,
                RespectRecentAccess = true,
                RecentAccessWindowDays = 7,
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.MediaRetentionPolicies.Add(defaultPolicy);
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Created default media retention policy: {PolicyName} " +
                "(Positive: {PositiveDays}d, Zero: {ZeroDays}d, Negative: {NegativeDays}d)",
                defaultPolicy.Name,
                defaultPolicy.PositiveBalanceRetentionDays,
                defaultPolicy.ZeroBalanceRetentionDays,
                defaultPolicy.NegativeBalanceRetentionDays);
        }
    }
}