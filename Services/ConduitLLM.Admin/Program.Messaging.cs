using MassTransit;

namespace ConduitLLM.Admin;

public partial class Program
{
    /// <summary>
    /// Configures MassTransit event bus with RabbitMQ or in-memory transport.
    /// </summary>
    private static void ConfigureMessagingServices(WebApplicationBuilder builder, ILogger startupLogger)
    {
        // Configure RabbitMQ settings
        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>()
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Register MassTransit event bus for Admin API
        builder.Services.AddMassTransit(x =>
        {
            // Register consumers for Admin API cache invalidation
            x.AddConsumer<ConduitLLM.Core.Consumers.GlobalSettingCacheInvalidationHandler>();

            // Add Function Discovery Cache invalidation consumers
            x.AddConsumer<ConduitLLM.Core.Consumers.FunctionConfigurationCacheInvalidationHandler>();
            x.AddConsumer<ConduitLLM.Core.Consumers.FunctionDiscoveryCacheInvalidationRequestHandler>();

            if (useRabbitMq)
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    // Configure RabbitMQ connection with advanced settings
                    cfg.Host(new Uri($"rabbitmq://{rabbitMqConfig.Host}:{rabbitMqConfig.Port}{rabbitMqConfig.VHost}"), h =>
                    {
                        h.Username(rabbitMqConfig.Username);
                        h.Password(rabbitMqConfig.Password);
                        h.Heartbeat(TimeSpan.FromSeconds(rabbitMqConfig.RequestedHeartbeat));

                        // Publisher settings
                        h.PublisherConfirmation = rabbitMqConfig.PublisherConfirmation;

                        // Advanced connection settings for publishers
                        h.RequestedChannelMax(rabbitMqConfig.ChannelMax);
                    });

                    // Configure retry policy for publishing and consuming
                    cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));

                    // Configure endpoints including consumers
                    cfg.ConfigureEndpoints(context);
                });

                startupLogger.LogInformation(
                    "Event bus configured with RabbitMQ transport (multi-instance mode) — Host: {Host}:{Port}. Publishing and consuming enabled",
                    rabbitMqConfig.Host, rabbitMqConfig.Port);
            }
            else
            {
                x.UsingInMemory((context, cfg) =>
                {
                    // Configure retry policy for reliability
                    cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

                    // Configure delayed redelivery for failed messages
                    cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)));

                    // Configure endpoints
                    cfg.ConfigureEndpoints(context);
                });

                startupLogger.LogInformation("Event bus configured with in-memory transport (single-instance mode). Events will be processed locally");
                startupLogger.LogWarning("For production multi-instance deployments, configure RabbitMQ to ensure cross-instance cache invalidation");
            }
        });
    }
}
