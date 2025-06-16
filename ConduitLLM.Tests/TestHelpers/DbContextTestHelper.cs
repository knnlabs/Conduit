using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Helper class for creating and seeding in-memory databases for testing
    /// </summary>
    public static class DbContextTestHelper
    {
        /// <summary>
        /// Creates a ConfigurationDbContext with an in-memory database for testing
        /// </summary>
        /// <param name="dbName">Optional name for the database (if not provided, a random name is generated)</param>
        /// <returns>A ConfigurationDbContext connected to an in-memory database</returns>
        public static ConfigurationDbContext CreateInMemoryDbContext(string? dbName = null)
        {
            dbName ??= $"TestDb_{Guid.NewGuid()}";

            var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .EnableSensitiveDataLogging()
                .Options;

            return new ConfigurationDbContext(options);
        }

        /// <summary>
        /// Creates a DbContextFactory that creates in-memory database contexts
        /// </summary>
        /// <param name="dbName">Optional name for the database (if not provided, a random name is generated)</param>
        /// <returns>An IDbContextFactory<ConfigurationDbContext> that creates in-memory database contexts</returns>
        public static IDbContextFactory<ConfigurationDbContext> CreateInMemoryDbContextFactory(string? dbName = null)
        {
            dbName ??= $"TestDb_{Guid.NewGuid()}";

            // Create a service collection and add the DbContext factory
            var services = new ServiceCollection();
            services.AddDbContextFactory<ConfigurationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: dbName)
                      .EnableSensitiveDataLogging());

            // Build the service provider and get the factory
            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
        }

        /// <summary>
        /// Seeds a database context with test data
        /// </summary>
        /// <param name="context">The database context to seed</param>
        /// <param name="globalSettings">Optional global settings to add</param>
        /// <param name="requestLogs">Optional request logs to add</param>
        /// <param name="virtualKeys">Optional virtual keys to add</param>
        /// <param name="modelCosts">Optional model costs to add</param>
        /// <param name="routerConfigs">Optional router configurations to add</param>
        /// <param name="modelDeployments">Optional model deployments to add</param>
        /// <param name="fallbackConfigs">Optional fallback configurations to add</param>
        /// <returns>A Task representing the asynchronous operation</returns>
        public static async Task SeedDatabaseAsync(
            ConfigurationDbContext context,
            List<GlobalSetting>? globalSettings = null,
            List<RequestLog>? requestLogs = null,
            List<VirtualKey>? virtualKeys = null,
            List<ModelCost>? modelCosts = null,
            List<RouterConfigEntity>? routerConfigs = null,
            List<ModelDeploymentEntity>? modelDeployments = null,
            List<FallbackConfigurationEntity>? fallbackConfigs = null)
        {
            if (globalSettings != null && globalSettings.Count > 0)
            {
                await context.GlobalSettings.AddRangeAsync(globalSettings);
            }

            if (requestLogs != null && requestLogs.Count > 0)
            {
                await context.RequestLogs.AddRangeAsync(requestLogs);
            }

            if (virtualKeys != null && virtualKeys.Count > 0)
            {
                await context.VirtualKeys.AddRangeAsync(virtualKeys);
            }

            if (modelCosts != null && modelCosts.Count > 0)
            {
                await context.ModelCosts.AddRangeAsync(modelCosts);
            }

            if (routerConfigs != null && routerConfigs.Count > 0)
            {
                await context.RouterConfigurations.AddRangeAsync(routerConfigs);
            }

            if (modelDeployments != null && modelDeployments.Count > 0)
            {
                await context.ModelDeployments.AddRangeAsync(modelDeployments);
            }

            if (fallbackConfigs != null && fallbackConfigs.Count > 0)
            {
                await context.FallbackConfigurations.AddRangeAsync(fallbackConfigs);
            }

            await context.SaveChangesAsync();
        }
    }
}
