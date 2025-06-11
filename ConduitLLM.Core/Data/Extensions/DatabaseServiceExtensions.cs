using ConduitLLM.Core.Data.Health;
using ConduitLLM.Core.Data.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Core.Data.Extensions
{
    /// <summary>
    /// Extension methods for registering database-related services with the dependency injection container.
    /// </summary>
    public static class DatabaseServiceExtensions
    {
        /// <summary>
        /// Adds database connection services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add the services to.</param>
        /// <returns>The service collection for method chaining.</returns>
        /// <example>
        /// ```csharp
        /// services.AddConnectionStringManager();
        /// ```
        /// </example>
        public static IServiceCollection AddConnectionStringManager(this IServiceCollection services)
        {
            // Register the connection string manager as a singleton
            services.TryAddSingleton<IConnectionStringManager, ConnectionStringManager>();

            return services;
        }

        /// <summary>
        /// Adds database connection factory to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add the services to.</param>
        /// <returns>The service collection for method chaining.</returns>
        /// <example>
        /// ```csharp
        /// services.AddConnectionStringManager()
        ///         .AddDatabaseConnectionFactory();
        /// ```
        /// </example>
        public static IServiceCollection AddDatabaseConnectionFactory(this IServiceCollection services)
        {
            // Make sure connection string manager is registered
            services.AddConnectionStringManager();

            // Register the database connection factory
            services.TryAddSingleton<IDatabaseConnectionFactory, DatabaseConnectionFactory>();

            return services;
        }

        /// <summary>
        /// Adds database initialization services for the specified DbContext type.
        /// </summary>
        /// <typeparam name="TContext">The type of DbContext to use.</typeparam>
        /// <param name="services">The service collection to add the services to.</param>
        /// <returns>The service collection for method chaining.</returns>
        /// <example>
        /// ```csharp
        /// services.AddConnectionStringManager()
        ///         .AddDatabaseInitializer<ApplicationDbContext>();
        /// ```
        /// </example>
        public static IServiceCollection AddDatabaseInitializer<TContext>(this IServiceCollection services)
            where TContext : DbContext
        {
            // Make sure connection string manager is registered
            services.AddConnectionStringManager();

            // Register database initializer
            services.TryAddScoped<IDatabaseInitializer, DatabaseInitializer<TContext>>();

            return services;
        }

        /// <summary>
        /// Adds all database-related services to the service collection.
        /// </summary>
        /// <typeparam name="TContext">The type of DbContext to use.</typeparam>
        /// <param name="services">The service collection to add the services to.</param>
        /// <returns>The service collection for method chaining.</returns>
        /// <example>
        /// ```csharp
        /// services.AddDatabaseServices<ApplicationDbContext>();
        /// ```
        /// </example>
        public static IServiceCollection AddDatabaseServices<TContext>(this IServiceCollection services)
            where TContext : DbContext
        {
            return services
                .AddConnectionStringManager()
                .AddDatabaseConnectionFactory()
                .AddDatabaseInitializer<TContext>()
                .AddDatabaseHealthChecks();
        }

        /// <summary>
        /// Adds database health checks to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add the services to.</param>
        /// <returns>The service collection for method chaining.</returns>
        public static IServiceCollection AddDatabaseHealthChecks(this IServiceCollection services)
        {
            // Add health checks for database
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database_health", tags: new[] { "database", "readiness" });

            return services;
        }
    }
}
