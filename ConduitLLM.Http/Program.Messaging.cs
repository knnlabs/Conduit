using ConduitLLM.Core.Services;

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
            // Provider health consumer removed
            
            // Add settings refresh consumers for runtime configuration updates
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ModelMappingCacheInvalidationHandler>();
            x.AddConsumer<ConduitLLM.Http.EventHandlers.ProviderCacheInvalidationHandler>();
            
            // Add media lifecycle handler for tracking generated media
            x.AddConsumer<ConduitLLM.Http.EventHandlers.MediaLifecycleHandler>();
            
            // Add video generation started handler for real-time notifications
            x.AddConsumer<ConduitLLM.Http.EventHandlers.VideoGenerationStartedHandler>();
            
            // Add webhook delivery consumer for scalable webhook processing
            x.AddConsumer<ConduitLLM.Http.Consumers.WebhookDeliveryConsumer>();
            
            // Add batch spend flush handler for admin operations and integration testing
            x.AddConsumer<ConduitLLM.Http.EventHandlers.BatchSpendFlushRequestedHandler>();
            
            // Add media lifecycle consumers for retention policy management
            x.AddConsumer<ConduitLLM.Http.Consumers.MediaRetentionPolicyConsumer>();
            x.AddConsumer<ConduitLLM.Http.Consumers.MediaCleanupBatchConsumer>();
            x.AddConsumer<ConduitLLM.Http.Consumers.R2BatchDeleteConsumer>();
            x.AddConsumer<ConduitLLM.Http.Consumers.MediaCleanupScheduleConsumer>();
            
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
                    
                    // Configure media retention policy evaluation endpoint
                    cfg.ReceiveEndpoint("media-retention-checks", e =>
                    {
                        e.PrefetchCount = 5; // Low prefetch for controlled processing
                        e.ConcurrentMessageLimit = 3; // Limited concurrency
                        
                        e.SetQuorumQueue();
                        e.SetQueueArgument("x-max-length", 1000);
                        
                        e.UseMessageRetry(r => r.Incremental(3, 
                            TimeSpan.FromSeconds(2), 
                            TimeSpan.FromSeconds(5)));
                        
                        e.UseCircuitBreaker(cb =>
                        {
                            cb.TrackingPeriod = TimeSpan.FromMinutes(2);
                            cb.TripThreshold = 15; // 15% failure rate
                            cb.ActiveThreshold = 5;
                            cb.ResetInterval = TimeSpan.FromMinutes(10);
                        });
                        
                        e.ConfigureConsumer<ConduitLLM.Http.Consumers.MediaRetentionPolicyConsumer>(context);
                    });
                    
                    // Configure media cleanup batch processing endpoint
                    cfg.ReceiveEndpoint("media-cleanup-batches", e =>
                    {
                        e.PrefetchCount = 10;
                        e.ConcurrentMessageLimit = 5;
                        
                        e.SetQuorumQueue();
                        
                        e.UseMessageRetry(r => r.Immediate(2));
                        
                        e.ConfigureConsumer<ConduitLLM.Http.Consumers.MediaCleanupBatchConsumer>(context);
                    });
                    
                    // Configure R2 batch operations endpoint with strict rate limiting for free tier
                    cfg.ReceiveEndpoint("r2-batch-operations", e =>
                    {
                        e.PrefetchCount = 2; // Very low prefetch for R2 free tier
                        e.ConcurrentMessageLimit = 1; // Sequential processing to avoid rate limits
                        
                        e.SetQuorumQueue();
                        e.SetQueueArgument("x-single-active-consumer", true); // Single consumer for rate control
                        e.SetQueueArgument("x-max-length", 500); // Limit queue size
                        
                        // Exponential backoff for R2 rate limit handling
                        e.UseMessageRetry(r => r.Exponential(10, 
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromMinutes(10),
                            TimeSpan.FromSeconds(2)));
                        
                        // Rate limiting to stay within R2 free tier limits
                        e.UseRateLimit(5, TimeSpan.FromSeconds(1)); // Max 5 operations per second
                        
                        // Circuit breaker for R2 service issues
                        e.UseCircuitBreaker(cb =>
                        {
                            cb.TrackingPeriod = TimeSpan.FromMinutes(5);
                            cb.TripThreshold = 20; // 20% failure rate
                            cb.ActiveThreshold = 10;
                            cb.ResetInterval = TimeSpan.FromMinutes(15);
                        });
                        
                        e.ConfigureConsumer<ConduitLLM.Http.Consumers.R2BatchDeleteConsumer>(context);
                    });
                    
                    // Configure media cleanup schedule endpoint
                    cfg.ReceiveEndpoint("media-cleanup-schedule", e =>
                    {
                        e.PrefetchCount = 1;
                        e.ConcurrentMessageLimit = 1; // Single processing for schedules
                        
                        e.SetQuorumQueue();
                        e.SetQueueArgument("x-single-active-consumer", true);
                        
                        e.UseMessageRetry(r => r.Immediate(2));
                        
                        e.ConfigureConsumer<ConduitLLM.Http.Consumers.MediaCleanupScheduleConsumer>(context);
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
                Console.WriteLine("  - Media lifecycle management (retention policies and cleanup)");
                Console.WriteLine("  - R2 batch operations (rate-limited for free tier compliance)");
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