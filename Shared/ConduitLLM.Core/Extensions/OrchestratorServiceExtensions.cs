using System;
using ConduitLLM.Core.Services;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering orchestrator services.
    /// </summary>
    public static class OrchestratorServiceExtensions
    {
        /// <summary>
        /// Registers media generation orchestrators.
        /// </summary>
        public static IServiceCollection AddMediaOrchestrators(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register orchestrators
            services.AddScoped<ImageGenerationOrchestrator>();
            services.AddScoped<VideoGenerationOrchestrator>();

            return services;
        }

        /// <summary>
        /// Configures MassTransit consumers.
        /// </summary>
        public static void ConfigureOrchestratorConsumers(
            this IBusRegistrationConfigurator configurator,
            IServiceProvider serviceProvider)
        {
            // Add consumers
            configurator.AddConsumer<ImageGenerationOrchestrator>();
            configurator.AddConsumer<VideoGenerationOrchestrator>();
        }

        /// <summary>
        /// Configures MassTransit endpoints.
        /// </summary>
        public static void ConfigureOrchestratorEndpoints(
            this IReceiveEndpointConfigurator endpointConfigurator,
            IBusRegistrationContext context,
            IServiceProvider serviceProvider)
        {
            // Configure endpoints
            endpointConfigurator.ConfigureConsumer<ImageGenerationOrchestrator>(context);
            endpointConfigurator.ConfigureConsumer<VideoGenerationOrchestrator>(context);
        }

        /// <summary>
        /// Example usage in Program.cs or Startup.cs
        /// </summary>
        public static void ExampleUsage(IServiceCollection services, IConfiguration configuration)
        {
            // Add orchestrators with feature flag support
            services.AddMediaOrchestrators(configuration);

            // Configure MassTransit
            services.AddMassTransit(x =>
            {
                // Let feature flags determine which consumers to add
                x.ConfigureOrchestratorConsumers(services.BuildServiceProvider());

                // Note: Transport configuration (RabbitMQ, InMemory, etc.) would be done
                // in the actual Program.cs or Startup.cs file, not here
                // Example:
                // x.UsingRabbitMq((context, cfg) => { ... });
            });
        }
    }
}