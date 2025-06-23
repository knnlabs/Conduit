using System;
using MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.EventHandlers;

namespace ConduitLLM.Http.Configuration
{
    /// <summary>
    /// Configures MassTransit with proper partitioning for ordered event processing.
    /// This ensures events are processed in order per virtual key, preventing race conditions.
    /// </summary>
    public static class MassTransitPartitioningConfiguration
    {
        /// <summary>
        /// Configure partitioned endpoints for video generation events.
        /// </summary>
        public static void ConfigureVideoGenerationEndpoints(
            this IRabbitMqBusFactoryConfigurator cfg, 
            IRegistrationContext context)
        {
            // Configure send topology for VideoGenerationRequested to use partitioning
            cfg.Send<VideoGenerationRequested>(x =>
            {
                // Partition by VirtualKeyId to ensure ordered processing per virtual key
                x.UsePartitioner(p => p.Message.VirtualKeyId);
            });

            // Configure video generation endpoint with partitioning
            cfg.ReceiveEndpoint("video-generation", e =>
            {
                // Configure partitioner for the endpoint
                e.ConfigurePartitioner<VideoGenerationRequested>(
                    context, 
                    p => p.Message.VirtualKeyId);

                // Configure consumer
                e.ConfigureConsumer<VideoGenerationOrchestrator>(context);

                // Configure retry policy specific to video generation
                e.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMinutes(2),
                    TimeSpan.FromMinutes(10)
                ));

                // Configure concurrency limit for video generation
                e.ConcurrentMessageLimit = 5; // Limit concurrent video generations
            });

            // Configure progress update endpoint
            cfg.ReceiveEndpoint("video-progress", e =>
            {
                e.ConfigureConsumer<VideoGenerationProgressHandler>(context);
                
                // Progress updates can be processed concurrently
                e.ConcurrentMessageLimit = 20;
            });

            // Configure completion endpoint
            cfg.ReceiveEndpoint("video-completion", e =>
            {
                // Configure partitioner to ensure ordered processing per virtual key
                e.ConfigurePartitioner<VideoGenerationCompleted>(
                    context,
                    p => p.Message.PartitionKey);

                e.ConfigureConsumer<VideoGenerationCompletedHandler>(context);
            });

            // Configure failure endpoint
            cfg.ReceiveEndpoint("video-failure", e =>
            {
                e.ConfigureConsumer<VideoGenerationFailedHandler>(context);
                
                // Configure dead letter queue for persistent failures
                e.UseScheduledRedelivery(r => r.Intervals(
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(2)
                ));
            });
        }

        /// <summary>
        /// Configure partitioned endpoints for image generation events.
        /// </summary>
        public static void ConfigureImageGenerationEndpoints(
            this IRabbitMqBusFactoryConfigurator cfg,
            IRegistrationContext context)
        {
            // Configure send topology for ImageGenerationRequested to use partitioning
            cfg.Send<ImageGenerationRequested>(x =>
            {
                // Partition by VirtualKeyId to ensure ordered processing per virtual key
                x.UsePartitioner(p => p.Message.VirtualKeyId.ToString());
            });

            // Configure image generation endpoint with partitioning
            cfg.ReceiveEndpoint("image-generation", e =>
            {
                // Configure partitioner for the endpoint
                e.ConfigurePartitioner<ImageGenerationRequested>(
                    context,
                    p => p.Message.VirtualKeyId.ToString());

                // Configure consumer
                e.ConfigureConsumer<ImageGenerationOrchestrator>(context);

                // Configure retry policy
                e.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMinutes(2)
                ));

                // Higher concurrency for images than videos
                e.ConcurrentMessageLimit = 10;
            });
        }

        /// <summary>
        /// Configure partitioned endpoints for spend update events.
        /// </summary>
        public static void ConfigureSpendUpdateEndpoints(
            this IRabbitMqBusFactoryConfigurator cfg,
            IRegistrationContext context)
        {
            // Configure send topology for SpendUpdateRequested to use partitioning
            cfg.Send<SpendUpdateRequested>(x =>
            {
                // Partition by VirtualKeyId to prevent race conditions
                x.UsePartitioner(p => p.Message.VirtualKeyId.ToString());
            });

            // Configure spend update endpoint with partitioning
            cfg.ReceiveEndpoint("spend-update", e =>
            {
                // Configure partitioner for ordered processing per virtual key
                e.ConfigurePartitioner<SpendUpdateRequested>(
                    context,
                    p => p.Message.VirtualKeyId.ToString());

                // Configure consumer
                e.ConfigureConsumer<SpendUpdateProcessor>(context);

                // No retry for spend updates - they should be idempotent
                e.UseMessageRetry(r => r.None());
            });
        }

        /// <summary>
        /// Configure in-memory endpoints (for single-instance deployments).
        /// </summary>
        public static void ConfigureInMemoryEndpoints(
            this IInMemoryBusFactoryConfigurator cfg,
            IRegistrationContext context)
        {
            // In-memory doesn't support true partitioning, but we can still
            // configure the endpoints for consistency
            
            // Video generation endpoint
            cfg.ReceiveEndpoint("video-generation", e =>
            {
                e.ConfigureConsumer<VideoGenerationOrchestrator>(context);
                
                // Configure retry policy
                e.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMinutes(2),
                    TimeSpan.FromMinutes(10)
                ));
            });

            // Progress endpoint
            cfg.ReceiveEndpoint("video-progress", e =>
            {
                e.ConfigureConsumer<VideoGenerationProgressHandler>(context);
            });

            // Completion endpoint
            cfg.ReceiveEndpoint("video-completion", e =>
            {
                e.ConfigureConsumer<VideoGenerationCompletedHandler>(context);
            });

            // Failure endpoint
            cfg.ReceiveEndpoint("video-failure", e =>
            {
                e.ConfigureConsumer<VideoGenerationFailedHandler>(context);
            });

            // Image generation endpoint
            cfg.ReceiveEndpoint("image-generation", e =>
            {
                e.ConfigureConsumer<ImageGenerationOrchestrator>(context);
            });

            // Spend update endpoint
            cfg.ReceiveEndpoint("spend-update", e =>
            {
                e.ConfigureConsumer<SpendUpdateProcessor>(context);
            });
        }
    }
}