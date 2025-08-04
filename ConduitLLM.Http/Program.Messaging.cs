using ConduitLLM.Configuration;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Extensions;
using MassTransit;

public partial class Program
{
    public static void ConfigureMessagingServices(WebApplicationBuilder builder)
    {
        // Configure RabbitMQ settings
        var rabbitMqConfig = builder.Configuration.GetSection("ConduitLLM:RabbitMQ").Get<ConduitLLM.Configuration.RabbitMqConfiguration>() 
            ?? new ConduitLLM.Configuration.RabbitMqConfiguration();

        // Check if RabbitMQ is configured
        var useRabbitMq = !string.IsNullOrEmpty(rabbitMqConfig.Host) && rabbitMqConfig.Host != "localhost";

        // Register MassTransit event bus
        builder.Services.AddMassTransit(x =>
        {
            // Add event consumers for Core API
            x.AddConsumer<ConduitLLM.Http.EventHandlers.VirtualKeyCacheInvalidationHandler>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.SpendUpdateProcessor>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ProviderEventHandler>();
            
            // Add spend notification consumer
            x.AddConsumer<ConduitLLM.Http.EventHandlers.SpendUpdatedHandler>();
            
            // Add model capabilities consumer
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ModelCapabilitiesDiscoveredHandler>();
            
            // Add image generation consumers
            x.AddConsumer<ConduitLLM.Core.Services.ImageGenerationOrchestrator>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ImageGenerationProgressHandler>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ImageGenerationCompletedHandler>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ImageGenerationFailedHandler>();
            
            // Add video generation consumers
            x.AddConsumer<ConduitLLM.Core.Services.VideoGenerationOrchestrator>();
            x.AddConsumer<ConduitLLM.Core.Services.VideoProgressTrackingOrchestrator>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationProgressHandler>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationCompletedHandler>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationFailedHandler>();
            
            // Add Admin API event consumers for cache invalidation
            x.AddConsumer<ConduitLLM.Http.Consumers.GlobalSettingCacheInvalidationHandler>();
            x.AddConsumer<ConduitLLM.Http.Consumers.IpFilterCacheInvalidationHandler>();
            
            // Add async task cache invalidation handler
            x.AddConsumer<ConduitLLM.Http.EventHandlers.AsyncTaskCacheInvalidationHandler>();
            x.AddConsumer<ConduitLLM.Http.Consumers.ModelCostCacheInvalidationHandler>();
            
            // Add navigation state event consumers for real-time updates
            x.AddConsumer<ConduitLLM.Http.Consumers.ModelMappingChangedNotificationConsumer>();
            x.AddConsumer<ConduitLLM.Http.Consumers.ProviderHealthChangedNotificationConsumer>();
            x.AddConsumer<ConduitLLM.Http.Consumers.ModelCapabilitiesDiscoveredNotificationConsumer>();
            
            // Add settings refresh consumers for runtime configuration updates
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ModelMappingCacheInvalidationHandler>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ProviderCacheInvalidationHandler>();
            
            // Add media lifecycle handler for tracking generated media
            x.AddConsumer<ConduitLLM.Http.EventHandlers.MediaLifecycleHandler>();
            
            // Add video generation started handler for real-time notifications
            x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationStartedHandler>();
            
            // Add webhook delivery consumer for scalable webhook processing
            x.AddConsumer<ConduitLLM.Http.Consumers.WebhookDeliveryConsumer>();
            
            // Add model discovery notification handler for real-time model updates
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ModelDiscoveryNotificationHandler>();
            
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
                    cfg.ReceiveEndpoint("webhook-delivery", e =>
                    {
                        // Configure for high throughput - balanced for memory usage
                        e.PrefetchCount = 100; // Reduced from 200 to prevent memory overload
                        e.ConcurrentMessageLimit = 75; // Balanced concurrency
                        
                        // Use quorum queue for better reliability
                        e.SetQuorumQueue();
                        e.SetQueueArgument("x-delivery-limit", 10); // Max redelivery attempts
                        e.SetQueueArgument("x-max-length", 50000); // Queue size limit
                        e.SetQueueArgument("x-overflow", "reject-publish"); // Reject new messages when full
                        
                        // Configure retry with exponential backoff
                        e.UseMessageRetry(r => r.Exponential(3, 
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(30),
                            TimeSpan.FromSeconds(2)));
                        
                        // Circuit breaker to prevent cascading failures
                        e.UseCircuitBreaker(cb =>
                        {
                            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                            cb.TripThreshold = 15; // 15% failure rate
                            cb.ActiveThreshold = 10; // Minimum attempts before evaluating
                            cb.ResetInterval = TimeSpan.FromMinutes(5);
                        });
                        
                        // Rate limiting to prevent consumer overload
                        e.UseRateLimit(100, TimeSpan.FromSeconds(1)); // 100 messages per second
                        
                        // Prevents duplicate sends during retries
                        // Note: UseInMemoryOutbox is now configured at the bus level
                        
                        e.ConfigureConsumer<ConduitLLM.Http.Consumers.WebhookDeliveryConsumer>(context, c =>
                        {
                            c.UseConcurrentMessageLimit(75);
                        });
                    });
                    
                    // Configure video generation endpoint for high throughput
                    cfg.ReceiveEndpoint("video-generation-events", e =>
                    {
                        e.PrefetchCount = rabbitMqConfig.PrefetchCount;
                        e.ConcurrentMessageLimit = rabbitMqConfig.ConcurrentMessageLimit;
                        
                        // Enable consume topology to properly bind consumers to the queue
                        // This ensures VideoGenerationRequested events are routed to this endpoint
                        e.ConfigureConsumeTopology = true;
                        e.SetQuorumQueue();
                        // Note: Removed x-single-active-consumer as it conflicts with partitioned processing
                        // Ordering is maintained through partition keys in the event messages
                        
                        // Retry policy for transient failures
                        e.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)));
                        
                        // Circuit breaker
                        e.UseCircuitBreaker(cb =>
                        {
                            cb.TrackingPeriod = TimeSpan.FromMinutes(2);
                            cb.TripThreshold = 20; // 20% failure rate
                            cb.ActiveThreshold = 5;
                            cb.ResetInterval = TimeSpan.FromMinutes(10);
                        });
                        
                        e.ConfigureConsumer<ConduitLLM.Core.Services.VideoGenerationOrchestrator>(context);
                        e.ConfigureConsumer<ConduitLLM.Core.Services.VideoProgressTrackingOrchestrator>(context);
                    });
                    
                    // Configure image generation endpoint
                    cfg.ReceiveEndpoint("image-generation-events", e =>
                    {
                        e.PrefetchCount = rabbitMqConfig.PrefetchCount;
                        e.ConcurrentMessageLimit = rabbitMqConfig.ConcurrentMessageLimit;
                        
                        e.SetQuorumQueue();
                        e.SetQueueArgument("x-single-active-consumer", true);
                        
                        e.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)));
                        
                        e.UseCircuitBreaker(cb =>
                        {
                            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                            cb.TripThreshold = 15;
                            cb.ActiveThreshold = 5;
                            cb.ResetInterval = TimeSpan.FromMinutes(5);
                        });
                        
                        e.ConfigureConsumer<ConduitLLM.Core.Services.ImageGenerationOrchestrator>(context);
                    });
                    
                    // Configure spend update endpoint with strict ordering
                    cfg.ReceiveEndpoint("spend-update-events", e =>
                    {
                        e.PrefetchCount = 10; // Lower prefetch for ordered processing
                        e.ConcurrentMessageLimit = 1; // Sequential processing per partition
                        
                        e.SetQuorumQueue();
                        e.SetQueueArgument("x-single-active-consumer", true);
                        e.SetQueueArgument("x-max-length", 10000);
                        
                        e.UseMessageRetry(r => r.Immediate(3));
                        
                        e.ConfigureConsumer<ConduitLLM.Http.EventHandlers.SpendUpdateProcessor>(context);
                    });
                    
                    // Configure dead letter exchange at the endpoint level
                    // Dead letter queues are configured per endpoint above
                    
                    // Configure remaining endpoints with automatic topology
                    cfg.ConfigureEndpoints(context);
                });
                
                Console.WriteLine($"[Conduit] Event bus configured with RabbitMQ transport (multi-instance mode) - Host: {rabbitMqConfig.Host}:{rabbitMqConfig.Port}");
                Console.WriteLine("[Conduit] Event-driven architecture ENABLED - Services will publish events for:");
                Console.WriteLine("  - Virtual Key updates (cache invalidation across instances)");
                Console.WriteLine("  - Spend updates (ordered processing with race condition prevention)");
                Console.WriteLine("  - Provider credential changes (automatic capability refresh)");
                Console.WriteLine("  - Model capability discovery (shared across all instances)");
                Console.WriteLine("  - Model mapping changes (real-time WebUI updates via SignalR)");
                Console.WriteLine("  - Provider health changes (real-time WebUI updates via SignalR)");
                Console.WriteLine("  - Global settings changes (system-wide configuration updates)");
                Console.WriteLine("  - IP filter changes (security policy updates)");
                Console.WriteLine("  - Model cost changes (pricing updates)");
                Console.WriteLine("  - Video generation tasks (partitioned processing per virtual key)");
                Console.WriteLine("  - Image generation tasks (partitioned processing per virtual key)");
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
                        
                        e.ConfigureConsumer<ConduitLLM.Http.Consumers.WebhookDeliveryConsumer>(context, c =>
                        {
                            // Configure consumer concurrency for in-memory
                            c.UseConcurrentMessageLimit(50); // Lower for single instance
                        });
                    });
                    
                    // Configure endpoints with automatic topology
                    cfg.ConfigureEndpoints(context);
                });
                
                Console.WriteLine("[Conduit] Event bus configured with in-memory transport (single-instance mode)");
                Console.WriteLine("[Conduit] Event-driven architecture ENABLED - Services will publish events locally");
                Console.WriteLine("[Conduit] WARNING: For production multi-instance deployments, configure RabbitMQ:");
                Console.WriteLine("  - Set CONDUITLLM__RABBITMQ__HOST to your RabbitMQ host");
                Console.WriteLine("  - Set CONDUITLLM__RABBITMQ__USERNAME and CONDUITLLM__RABBITMQ__PASSWORD");
                Console.WriteLine("  - This enables cache consistency and ordered processing across instances");
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
            Console.WriteLine("[Conduit] Batch webhook publisher configured for high-throughput delivery");
        }
    }
}