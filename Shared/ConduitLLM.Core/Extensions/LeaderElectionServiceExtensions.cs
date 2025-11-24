using System;
using System.Linq;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuring leader election services
    /// </summary>
    public static class LeaderElectionServiceExtensions
    {
        /// <summary>
        /// Adds leader election service to the service collection
        /// </summary>
        public static IServiceCollection AddLeaderElection(this IServiceCollection services)
        {
            // Register the leader election service as singleton
            services.AddSingleton<ILeaderElectionService>(serviceProvider =>
            {
                var redis = serviceProvider.GetService<IConnectionMultiplexer>();
                var logger = serviceProvider.GetRequiredService<ILogger<RedisLeaderElectionService>>();

                if (redis != null)
                {
                    // Use Redis-based leader election if Redis is available
                    return new RedisLeaderElectionService(redis, logger);
                }
                else
                {
                    // Fall back to in-memory implementation for development
                    var inMemoryLogger = serviceProvider.GetRequiredService<ILogger<InMemoryLeaderElectionService>>();
                    return new InMemoryLeaderElectionService(inMemoryLogger);
                }
            });

            return services;
        }

        /// <summary>
        /// Adds a hosted service with leader election
        /// </summary>
        /// <typeparam name="TService">The type of hosted service to add</typeparam>
        /// <param name="services">The service collection</param>
        /// <param name="serviceName">Optional unique name for the service (defaults to type name)</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddLeaderElectedHostedService<TService>(
            this IServiceCollection services,
            string? serviceName = null)
            where TService : class, IHostedService
        {
            // Register the actual service
            services.AddSingleton<TService>();

            // Register the wrapper as a hosted service
            // Use AddSingleton instead of AddHostedService to allow multiple instances
            // AddHostedService uses TryAddEnumerable which only registers the first LeaderElectedServiceWrapper
            services.AddSingleton<IHostedService>(serviceProvider =>
            {
                var innerService = serviceProvider.GetRequiredService<TService>();
                var leaderElectionService = serviceProvider.GetRequiredService<ILeaderElectionService>();
                var logger = serviceProvider.GetRequiredService<ILogger<Services.LeaderElectedServiceWrapper>>();
                var name = serviceName ?? typeof(TService).Name;

                return new Services.LeaderElectedServiceWrapper(innerService, leaderElectionService, logger, name);
            });

            return services;
        }

        /// <summary>
        /// Adds a hosted service with leader election using a factory
        /// </summary>
        public static IServiceCollection AddLeaderElectedHostedService<TService>(
            this IServiceCollection services,
            Func<IServiceProvider, TService> implementationFactory,
            string? serviceName = null)
            where TService : class, IHostedService
        {
            // Register the wrapper as a hosted service
            // Use AddSingleton instead of AddHostedService to allow multiple instances
            // AddHostedService uses TryAddEnumerable which only registers the first LeaderElectedServiceWrapper
            services.AddSingleton<IHostedService>(serviceProvider =>
            {
                var innerService = implementationFactory(serviceProvider);
                var leaderElectionService = serviceProvider.GetRequiredService<ILeaderElectionService>();
                var logger = serviceProvider.GetRequiredService<ILogger<Services.LeaderElectedServiceWrapper>>();
                var name = serviceName ?? typeof(TService).Name;

                return new Services.LeaderElectedServiceWrapper(innerService, leaderElectionService, logger, name);
            });

            return services;
        }

        /// <summary>
        /// Converts an existing hosted service registration to use leader election
        /// </summary>
        public static IServiceCollection ConvertToLeaderElected<TService>(
            this IServiceCollection services,
            string? serviceName = null)
            where TService : class, IHostedService
        {
            // Find and remove the existing hosted service registration
            var hostedServiceDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(TService));

            if (hostedServiceDescriptor != null)
            {
                services.Remove(hostedServiceDescriptor);
            }

            // Add it back with leader election
            return services.AddLeaderElectedHostedService<TService>(serviceName);
        }
    }
}