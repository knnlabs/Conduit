using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.MassTransit;
using ConduitLLM.Configuration.Messaging.Wolverine;

using MassTransit;

namespace ConduitLLM.Admin;

public partial class Program
{
    /// <summary>
    /// Configures the event bus: MassTransit (RabbitMQ or in-memory) by default, or
    /// Wolverine on the PostgreSQL transport when selected by
    /// <c>ConduitLLM:Messaging:Backend</c> (epic #909 Phase 2).
    /// </summary>
    private static void ConfigureMessagingServices(WebApplicationBuilder builder, ILogger startupLogger)
    {
        // Backend-neutral: the shared cache-invalidation IEventHandler<T> implementations (#919).
        ConduitLLM.Core.Extensions.SharedCacheInvalidationMessagingExtensions.AddSharedCacheInvalidationHandlers(builder.Services);

        // Phase 2 backend switch (#924/#925): ConduitLLM:Messaging:Backend selects the
        // host for the abstraction. Wolverine runs on the PostgreSQL transport with
        // durable persistence; MassTransit (default) keeps the Phase 1 wiring unchanged.
        if (MessagingBackendResolver.Resolve(builder.Configuration) == MessagingBackend.Wolverine)
        {
            builder.Services.AddWolverineEventBus();

            var (_, wolverineConnectionString) = new ConduitLLM.Core.Data.ConnectionStringManager()
                .GetProviderAndConnectionString("AdminAPI", msg => startupLogger.LogInformation("{Message}", msg));

            builder.Host.AddConduitWolverine(builder.Configuration, wolverineConnectionString, "conduit-admin", opts =>
            {
                ConduitLLM.Core.Extensions.SharedCacheInvalidationMessagingExtensions.AddSharedCacheInvalidationBridges(opts);
            });

            startupLogger.LogInformation(
                "Event bus configured with the Wolverine backend (PostgreSQL transport, durable persistence). " +
                "Cross-service queue topology follows in #926");
            return;
        }

        // Register the Conduit-owned IEventBus abstraction over MassTransit (epic #909).
        builder.Services.AddMassTransitEventBus();

        // Configure RabbitMQ settings
        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>()
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Register MassTransit event bus for Admin API
        builder.Services.AddMassTransit(x =>
        {
            // Cache-invalidation handlers (#919) are migrated to IEventHandler<T> and
            // dispatched via the generic bridge. Handlers are registered on builder.Services.
            ConduitLLM.Core.Extensions.SharedCacheInvalidationMessagingExtensions.AddSharedCacheInvalidationBridges(x);

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
