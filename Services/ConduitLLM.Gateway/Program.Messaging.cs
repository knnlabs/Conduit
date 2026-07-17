using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.MassTransit;
using ConduitLLM.Configuration.Messaging.Wolverine;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;

public partial class Program
{
    public static void ConfigureMessagingServices(WebApplicationBuilder builder)
    {
        // Backend-neutral handler registrations — identical for both backends.
        //
        // Cache-invalidation / notification IEventHandler<T> implementations (#919):
        ConduitLLM.Gateway.Extensions.CacheInvalidationMessagingExtensions.AddGatewayCacheInvalidationHandlers(builder.Services);
        ConduitLLM.Core.Extensions.SharedCacheInvalidationMessagingExtensions.AddSharedCacheInvalidationHandlers(builder.Services);

        // Media-generation orchestrator / notification IEventHandler<T> implementations
        // (#920). On MassTransit the orchestrator bridges are bound to the tuned
        // image-/video-generation endpoints below.
        ConduitLLM.Gateway.Extensions.MediaGenerationMessagingExtensions.AddMediaGenerationHandlers(builder.Services);

        // High-risk handlers (#921): ordered spend processing (spend-update-events),
        // deferred-retry webhook delivery (webhook-delivery), and the batch spend flush.
        builder.Services.AddEventHandler<ConduitLLM.Core.Events.SpendUpdateRequested, ConduitLLM.Gateway.EventHandlers.SpendUpdateProcessor>();
        builder.Services.AddEventHandler<ConduitLLM.Configuration.Events.BatchSpendFlushRequestedEvent, ConduitLLM.Gateway.EventHandlers.BatchSpendFlushRequestedHandler>();
        builder.Services.AddEventHandler<ConduitLLM.Core.Events.WebhookDeliveryRequested, ConduitLLM.Gateway.Consumers.WebhookDeliveryConsumer>();

        // Phase 2 backend switch (#924/#925): ConduitLLM:Messaging:Backend selects the
        // host for the abstraction. Wolverine runs on the PostgreSQL transport with
        // durable persistence; MassTransit (default) keeps the Phase 1 wiring unchanged.
        if (MessagingBackendResolver.Resolve(builder.Configuration) == MessagingBackend.Wolverine)
        {
            ConfigureWolverineMessaging(builder);
            return;
        }

        // Register the Conduit-owned IEventBus abstraction over MassTransit (epic #909).
        // Scoped so follow-on publishes inside a consume scope stay correlation-aware,
        // exactly as injecting IPublishEndpoint behaved before.
        builder.Services.AddMassTransitEventBus();

        // Configure RabbitMQ settings
        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>() 
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Register MassTransit event bus
        builder.Services.AddMassTransit(x =>
        {
            // Cache-invalidation / notification handlers (#919) are migrated to IEventHandler<T>
            // and dispatched through the generic MassTransit bridge instead of per-consumer
            // AddConsumer registrations. Bridges register one consumer per event type; the
            // handler implementations are registered on builder.Services (see top of method).
            ConduitLLM.Gateway.Extensions.CacheInvalidationMessagingExtensions.AddGatewayCacheInvalidationBridges(x);
            ConduitLLM.Core.Extensions.SharedCacheInvalidationMessagingExtensions.AddSharedCacheInvalidationBridges(x);

            // Media-generation orchestrators + progress/completed/failed handlers (#920),
            // dispatched through the generic bridge like the cache handlers above.
            ConduitLLM.Gateway.Extensions.MediaGenerationMessagingExtensions.AddMediaGenerationBridges(x);

            // High-risk bridges (#921): ordered spend processor (spend-update-events),
            // webhook delivery (webhook-delivery), batch spend flush (default endpoint).
            x.AddEventBridge<ConduitLLM.Core.Events.SpendUpdateRequested>();
            x.AddEventBridge<ConduitLLM.Core.Events.WebhookDeliveryRequested>();
            x.AddEventBridge<ConduitLLM.Configuration.Events.BatchSpendFlushRequestedEvent>();

            if (useRabbitMq)
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    // Configure RabbitMQ connection
                    cfg.Host(new Uri($"rabbitmq://{rabbitMqConfig.Host}:{rabbitMqConfig.Port}{rabbitMqConfig.VHost}"), h =>
                    {
                        h.Username(rabbitMqConfig.Username);
                        h.Password(rabbitMqConfig.Password);
                        h.Heartbeat(TimeSpan.FromSeconds(rabbitMqConfig.RequestedHeartbeat));
                        
                        // High throughput settings
                        h.PublisherConfirmation = rabbitMqConfig.PublisherConfirmation;
                        
                        // Advanced connection settings
                        h.RequestedChannelMax(rabbitMqConfig.ChannelMax);
                        h.RequestedConnectionTimeout(TimeSpan.FromSeconds(30));
                    });
                    
                    // Configure prefetch count for consumer concurrency
                    cfg.PrefetchCount = rabbitMqConfig.PrefetchCount;
                    
                    // Configure retry policy for reliability
                    cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
                    
                    // Configure delayed redelivery for failed messages
                    cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)));
                    
                    // Configure webhook delivery endpoint optimized for 1000+ webhooks/minute
                    // (#917/#921). Behavior comes from ConduitEndpointPolicies.WebhookDelivery:
                    // prefetch 100 / concurrency 75, quorum queue with delivery-limit/max-length/
                    // overflow arguments, exponential retry, circuit breaker, rate limit 100/s.
                    cfg.ReceiveEndpoint(ConduitEndpointPolicies.WebhookDelivery.Name, e =>
                    {
                        ApplyRabbitMqEndpointPolicy(e, ConduitEndpointPolicies.WebhookDelivery, rabbitMqConfig);

                        // Prevents duplicate sends during retries
                        // Note: UseInMemoryOutbox is now configured at the bus level

                        e.ConfigureConsumer<MassTransitConsumerBridge<WebhookDeliveryRequested>>(context, c =>
                        {
                            c.UseConcurrentMessageLimit(75);
                        });
                    });
                    
                    // Configure video generation endpoint for high throughput (#917/#920).
                    // Behavior comes from the ConduitEndpointPolicies.VideoGeneration descriptor:
                    // partition-key ordering (no single-active-consumer), incremental retry,
                    // circuit breaker; prefetch/concurrency inherit ConduitLLM:RabbitMQ config.
                    cfg.ReceiveEndpoint(ConduitEndpointPolicies.VideoGeneration.Name, e =>
                    {
                        ApplyRabbitMqEndpointPolicy(e, ConduitEndpointPolicies.VideoGeneration, rabbitMqConfig);

                        // The video orchestrator consumed VideoGenerationRequested AND
                        // VideoGenerationCancelled on this endpoint; the bridges preserve that.
                        e.ConfigureConsumer<MassTransitConsumerBridge<VideoGenerationRequested>>(context);
                        e.ConfigureConsumer<MassTransitConsumerBridge<VideoGenerationCancelled>>(context);
                        e.ConfigureConsumer<MassTransitConsumerBridge<VideoProgressCheckRequested>>(context);
                    });

                    // Configure image generation endpoint (#917/#920): single-active-consumer,
                    // incremental retry, circuit breaker — from ConduitEndpointPolicies.ImageGeneration.
                    cfg.ReceiveEndpoint(ConduitEndpointPolicies.ImageGeneration.Name, e =>
                    {
                        ApplyRabbitMqEndpointPolicy(e, ConduitEndpointPolicies.ImageGeneration, rabbitMqConfig);

                        e.ConfigureConsumer<MassTransitConsumerBridge<ImageGenerationRequested>>(context);
                        e.ConfigureConsumer<MassTransitConsumerBridge<ImageGenerationCancelled>>(context);
                    });
                    
                    // Configure spend update endpoint with strict ordering (#917/#921):
                    // prefetch 10, concurrency 1, single-active-consumer, immediate retry x3
                    // — from ConduitEndpointPolicies.SpendUpdate. Ordering is RabbitMQ-native
                    // and MUST be preserved exactly.
                    cfg.ReceiveEndpoint(ConduitEndpointPolicies.SpendUpdate.Name, e =>
                    {
                        ApplyRabbitMqEndpointPolicy(e, ConduitEndpointPolicies.SpendUpdate, rabbitMqConfig);

                        e.ConfigureConsumer<MassTransitConsumerBridge<SpendUpdateRequested>>(context);
                    });

                    // Configure remaining endpoints with automatic topology
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingInMemory((context, cfg) =>
                {
                    // NOTE: Using in-memory transport for single-instance deployments
                    // Configure RabbitMQ environment variables for multi-instance production
                    
                    // Configure retry policy for reliability
                    cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
                    
                    // Configure delayed redelivery for failed messages
                    cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)));
                    
                    // Configure webhook delivery endpoint with high throughput settings
                    cfg.ReceiveEndpoint("webhook-delivery", e =>
                    {
                        // Configure retry with shorter intervals for webhook scenarios
                        e.UseMessageRetry(r => r.Exponential(3, 
                            TimeSpan.FromSeconds(1), // Faster initial retry
                            TimeSpan.FromSeconds(30), // Max backoff
                            TimeSpan.FromSeconds(2)));
                        
                        // Prevents duplicate sends during retries
                        // Note: UseInMemoryOutbox is now configured at the bus level

                        e.ConfigureConsumer<MassTransitConsumerBridge<WebhookDeliveryRequested>>(context, c =>
                        {
                            // Configure consumer concurrency for in-memory
                            c.UseConcurrentMessageLimit(50); // Lower for single instance
                        });
                    });
                    
                    // Configure endpoints with automatic topology
                    cfg.ConfigureEndpoints(context);
                });

                Console.Error.WriteLine("[Conduit] WARNING: For production multi-instance deployments, configure RabbitMQ:");
                Console.Error.WriteLine("  - Set CONDUITLLM__RABBITMQ__HOST to your RabbitMQ host");
                Console.Error.WriteLine("  - Set CONDUITLLM__RABBITMQ__USERNAME and CONDUITLLM__RABBITMQ__PASSWORD");
                Console.Error.WriteLine("  - This enables cache consistency and ordered processing across instances");
            }
        });

        // Register batch webhook publisher for high-throughput webhook delivery
        if (useRabbitMq)
        {
            builder.Services.AddBatchWebhookPublisher(options =>
            {
                options.MaxBatchSize = 100;
                options.MaxBatchDelay = TimeSpan.FromMilliseconds(100);
                options.ConcurrentPublishers = 3;
            });
        }
    }

    /// <summary>
    /// Wolverine backend wiring (#925): IEventBus adapter + one bridge handler per
    /// bridged event type, on the PostgreSQL transport with durable persistence.
    /// Publishes route to the local durable queues of this process's bridges; the
    /// tuned endpoint policies (ordering, deferral, concurrency — the analogue of the
    /// RabbitMQ endpoints below) and cross-service queue topology land in I2.3/#926.
    /// </summary>
    private static void ConfigureWolverineMessaging(WebApplicationBuilder builder)
    {
        builder.Services.AddWolverineEventBus();

        var (_, connectionString) = new ConduitLLM.Core.Data.ConnectionStringManager()
            .GetProviderAndConnectionString("CoreAPI");

        builder.Host.AddConduitWolverine(builder.Configuration, connectionString, "conduit-gateway", opts =>
        {
            ConduitLLM.Gateway.Extensions.CacheInvalidationMessagingExtensions.AddGatewayCacheInvalidationBridges(opts);
            ConduitLLM.Core.Extensions.SharedCacheInvalidationMessagingExtensions.AddSharedCacheInvalidationBridges(opts);
            ConduitLLM.Gateway.Extensions.MediaGenerationMessagingExtensions.AddMediaGenerationBridges(opts);

            // High-risk bridges (#921): endpoint tuning for these (single/sequential
            // listener for spend ordering, webhook deferral/concurrency) follows in #926.
            opts.AddEventBridge<SpendUpdateRequested>();
            opts.AddEventBridge<WebhookDeliveryRequested>();
            opts.AddEventBridge<ConduitLLM.Configuration.Events.BatchSpendFlushRequestedEvent>();
        });

        // Batch webhook publisher: publishes via IEventBus, so it is backend-agnostic.
        // Registered unconditionally on Wolverine (there is no RabbitMQ to gate on).
        builder.Services.AddBatchWebhookPublisher(options =>
        {
            options.MaxBatchSize = 100;
            options.MaxBatchDelay = TimeSpan.FromMilliseconds(100);
            options.ConcurrentPublishers = 3;
        });
    }

    /// <summary>
    /// Applies the RabbitMQ-only transport settings of an <see cref="EndpointPolicy"/>
    /// descriptor (prefetch, concurrency, quorum queue, queue arguments,
    /// single-active-consumer, consume topology) plus its transport-neutral resilience
    /// middleware. Null prefetch/concurrency in the descriptor means "inherit the
    /// ConduitLLM:RabbitMQ configuration" (video/image endpoints).
    /// </summary>
    private static void ApplyRabbitMqEndpointPolicy(
        IRabbitMqReceiveEndpointConfigurator endpoint,
        EndpointPolicy policy,
        ConduitLLM.Configuration.RabbitMqConfiguration rabbitMqConfig)
    {
        endpoint.PrefetchCount = policy.PrefetchCount ?? rabbitMqConfig.PrefetchCount;
        endpoint.ConcurrentMessageLimit = policy.ConcurrentMessageLimit ?? rabbitMqConfig.ConcurrentMessageLimit;

        if (policy.ConfigureConsumeTopology)
        {
            endpoint.ConfigureConsumeTopology = true;
        }

        if (policy.QuorumQueue)
        {
            endpoint.SetQuorumQueue();
        }

        if (policy.SingleActiveConsumer)
        {
            endpoint.SetQueueArgument("x-single-active-consumer", true);
        }

        if (policy.QueueArguments is { } queueArguments)
        {
            foreach (var (key, value) in queueArguments)
            {
                endpoint.SetQueueArgument(key, value);
            }
        }

        MassTransitEndpointPolicy.ApplyResiliencePolicies(endpoint, policy);
    }
}