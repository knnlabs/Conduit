using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.ModelCatalogs;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Registration and startup entry points for database migration handling.
    /// Behavior is governed by CONDUIT_MIGRATION_MODE — see <see cref="MigrationMode"/>.
    /// </summary>
    public static class MigrationExtensions
    {
        /// <summary>
        /// Add migration services to DI container.
        /// </summary>
        public static IServiceCollection AddDatabaseMigration(this IServiceCollection services)
        {
            services.AddSingleton(sp => MigrationStartupOptions.FromEnvironment(
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("Conduit.MigrationStartup")));
            services.AddSingleton<MigrationReadinessState>();
            services.AddSingleton<IPendingMigrationsProbe, PendingMigrationsProbe>();
            services.AddSingleton<BundledModelCatalog>();
            services.AddScoped<IBundledModelCatalogImporter, BundledModelCatalogImporter>();
            services.AddScoped<SimpleMigrationService>();
            services.AddHostedService<MigrationWaitService>();
            return services;
        }

        /// <summary>
        /// Run migration startup handling per CONDUIT_MIGRATION_MODE:
        /// Apply migrates inline (advisory-lock protected) before the server starts;
        /// Wait returns immediately and lets <see cref="MigrationWaitService"/> gate
        /// readiness until an external migrator catches the schema up;
        /// Skip does nothing.
        /// </summary>
        public static async Task RunDatabaseMigrationAsync(this IHost app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SimpleMigrationService>>();
            var options = scope.ServiceProvider.GetRequiredService<MigrationStartupOptions>();
            var state = scope.ServiceProvider.GetRequiredService<MigrationReadinessState>();

            switch (options.Mode)
            {
                case MigrationMode.Skip:
                    logger.LogWarning(
                        "CONDUIT_MIGRATION_MODE=Skip: migrations and schema checks are disabled. " +
                        "Ensure your database schema is up to date!");
                    state.IsSchemaCurrent = true;
                    return;

                case MigrationMode.Wait:
                    // MigrationWaitService polls and flips readiness once the schema is
                    // current; the service starts serving with /health/ready failing.
                    logger.LogInformation(
                        "CONDUIT_MIGRATION_MODE=Wait: this instance will not migrate; readiness is gated until the schema is current.");
                    return;

                case MigrationMode.Apply:
                default:
                    try
                    {
                        logger.LogInformation("Running database migrations...");
                        var migrationService = scope.ServiceProvider.GetRequiredService<SimpleMigrationService>();
                        await migrationService.MigrateAsync();
                        state.IsSchemaCurrent = true;
                        logger.LogInformation("Database migrations completed successfully");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to run database migrations");
                        throw new InvalidOperationException("Database migration failed. Application cannot start.", ex);
                    }
                    return;
            }
        }
    }
}
