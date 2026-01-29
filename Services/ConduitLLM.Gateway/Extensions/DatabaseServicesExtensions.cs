using System.Text.RegularExpressions;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interceptors;
using ConduitLLM.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering database services
/// </summary>
public static class DatabaseServicesExtensions
{
    /// <summary>
    /// Adds database services including connection management, DbContext factory, and query monitoring
    /// </summary>
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Get connection string from environment variables
        var connectionStringManager = new ConnectionStringManager();
        // Pass "CoreAPI" to get Gateway API-specific connection pool settings
        var (dbProvider, dbConnectionString) = connectionStringManager.GetProviderAndConnectionString("CoreAPI", msg => Console.WriteLine(msg));

        // Log the connection pool settings for verification
        if (dbProvider == "postgres" && dbConnectionString.Contains("MaxPoolSize"))
        {
            Console.WriteLine($"[Conduit] Gateway API database connection pool configured:");
            var match = Regex.Match(dbConnectionString, @"MinPoolSize=(\d+);MaxPoolSize=(\d+)");
            if (match.Success)
            {
                Console.WriteLine($"[Conduit]   Min Pool Size: {match.Groups[1].Value}");
                Console.WriteLine($"[Conduit]   Max Pool Size: {match.Groups[2].Value}");
            }
        }

        // Only PostgreSQL is supported
        if (dbProvider != "postgres")
        {
            throw new InvalidOperationException($"Only PostgreSQL is supported. Invalid provider: {dbProvider}");
        }

        // Register DbContext Factory with query monitoring interceptor
        services.AddDbContextFactory<ConduitDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<QueryMonitoringInterceptor>();
            options.UseNpgsql(dbConnectionString)
                   .AddInterceptors(interceptor);
        });
        Console.WriteLine("[Conduit] Query monitoring interceptor configured for performance tracking");

        // Also add scoped registration from factory for services that need direct injection
        services.AddScoped<ConduitDbContext>(provider =>
        {
            var factory = provider.GetService<IDbContextFactory<ConduitDbContext>>();
            if (factory == null)
            {
                throw new InvalidOperationException("IDbContextFactory<ConfigurationDbContext> is not registered");
            }
            return factory.CreateDbContext();
        });

        return services;
    }
}
