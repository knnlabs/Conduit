using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.Wolverine;

using JasperFx.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.ModelCatalogs;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Entry point for the "migrate" CLI verb — the one schema-management moment per
    /// deploy. Intended for managed-platform release hooks (the hook runs the service
    /// image with command "migrate", e.g. <c>dotnet ConduitLLM.Admin.dll migrate</c>)
    /// while the services themselves run CONDUIT_MIGRATION_MODE=Wait.
    ///
    /// Applies EF Core migrations under the advisory lock, seeds default data, and —
    /// when the Wolverine backend is active on the Postgresql transport — provisions
    /// Wolverine's durability/queue schema. Builds only a minimal composition (console
    /// logging + DbContext), never the full web host.
    /// </summary>
    public static class MigrationCommand
    {
        /// <summary>The argv verb both service Mains recognize.</summary>
        public const string Verb = "migrate";

        public static bool Matches(string[] args)
            => args.Length > 0 && string.Equals(args[0], Verb, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Run the migrator. Returns a process exit code: 0 on success, 1 on failure.
        /// </summary>
        public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
        {
            using var loggerFactory = LoggerFactory.Create(b => b
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information));
            var logger = loggerFactory.CreateLogger("Conduit.Migrate");

            try
            {
                var options = MigrationStartupOptions.FromEnvironment(logger);
                var connectionString = ConfigurationDbContextFactory.ResolveNpgsqlConnectionString();

                var contextOptions = new DbContextOptionsBuilder<ConduitDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;
                var contextFactory = new FixedOptionsDbContextFactory(contextOptions);
                var catalogImporter = new BundledModelCatalogImporter(
                    contextFactory,
                    new BundledModelCatalog(),
                    loggerFactory.CreateLogger<BundledModelCatalogImporter>());
                var migrationService = new SimpleMigrationService(
                    contextFactory,
                    options,
                    loggerFactory.CreateLogger<SimpleMigrationService>(),
                    catalogImporter);

                await migrationService.MigrateAsync(cancellationToken);

                await ProvisionWolverineStorageAsync(connectionString, logger, cancellationToken);

                logger.LogInformation("Migration completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Migration failed");
                return 1;
            }
        }

        /// <summary>
        /// Provision Wolverine's durability and queue tables when the Wolverine backend
        /// is active on the Postgresql transport. Provisions regardless of the
        /// AutoProvision runtime setting — the migrate verb IS the explicit
        /// schema-management moment production relies on when AutoProvision=false.
        /// </summary>
        private static async Task ProvisionWolverineStorageAsync(
            string connectionString,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            if (MessagingBackendResolver.Resolve(configuration) != MessagingBackend.Wolverine)
            {
                logger.LogInformation("Messaging backend is not Wolverine; skipping bus schema provisioning");
                return;
            }

            if (WolverineMessagingExtensions.UsesInMemoryTransport(configuration))
            {
                logger.LogInformation("Wolverine transport is InMemory; no bus schema to provision");
                return;
            }

            logger.LogInformation("Provisioning Wolverine durability/queue schema...");

            // SetupResources resolves Wolverine's IStatefulResource registrations from
            // DI without starting the host — no durability agents, listeners, or node
            // leader election run.
            using var host = Host.CreateDefaultBuilder()
                .AddConduitWolverine(configuration, connectionString, "conduit-migrator")
                .Build();
            await host.SetupResources(cancellationToken);

            logger.LogInformation("Wolverine schema provisioned");
        }

        /// <summary>
        /// Minimal context factory over fixed options, so the migrator can reuse
        /// <see cref="SimpleMigrationService"/> without a DI container.
        /// </summary>
        private sealed class FixedOptionsDbContextFactory : IDbContextFactory<ConduitDbContext>
        {
            private readonly DbContextOptions<ConduitDbContext> _options;

            public FixedOptionsDbContextFactory(DbContextOptions<ConduitDbContext> options)
            {
                _options = options;
            }

            public ConduitDbContext CreateDbContext() => new(_options);
        }
    }
}
