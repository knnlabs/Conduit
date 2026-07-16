using ConduitLLM.Configuration.Messaging.MassTransit;
using ConduitLLM.Core.Events;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Registration for the media-generation orchestrators and their progress/completed/failed
    /// handlers, migrated to <c>IEventHandler&lt;T&gt;</c> in epic #909 (issue #920). The
    /// orchestrators (<c>ImageGenerationOrchestrator</c>, <c>VideoGenerationOrchestrator</c>,
    /// <c>VideoProgressTrackingOrchestrator</c>) consume on the tuned
    /// <c>image-generation-events</c> / <c>video-generation-events</c> endpoints — their
    /// bridges are bound there explicitly by <c>Program.Messaging.cs</c> — while the
    /// progress/completed/failed handlers ride the default (auto-configured) endpoints,
    /// exactly as their <c>IConsumer&lt;T&gt;</c> predecessors did.
    /// </summary>
    public static class MediaGenerationMessagingExtensions
    {
        /// <summary>
        /// Registers the media-generation <c>IEventHandler&lt;T&gt;</c> implementations.
        /// </summary>
        public static IServiceCollection AddMediaGenerationHandlers(this IServiceCollection services)
        {
            // Image generation orchestrator (request + cancellation on image-generation-events)
            services.AddEventHandler<ImageGenerationRequested, ConduitLLM.Core.Services.ImageGenerationOrchestrator>();
            services.AddEventHandler<ImageGenerationCancelled, ConduitLLM.Core.Services.ImageGenerationOrchestrator>();

            // Video generation orchestrators (request + cancellation + progress checks on video-generation-events)
            services.AddEventHandler<VideoGenerationRequested, ConduitLLM.Core.Services.VideoGenerationOrchestrator>();
            services.AddEventHandler<VideoGenerationCancelled, ConduitLLM.Core.Services.VideoGenerationOrchestrator>();
            services.AddEventHandler<VideoProgressCheckRequested, ConduitLLM.Core.Services.VideoProgressTrackingOrchestrator>();

            // Progress / completed / failed notification handlers (default endpoints)
            services.AddEventHandler<ImageGenerationProgress, Gateway.EventHandlers.ImageGenerationProgressHandler>();
            services.AddEventHandler<ImageGenerationCompleted, Gateway.EventHandlers.ImageGenerationCompletedHandler>();
            services.AddEventHandler<ImageGenerationFailed, Gateway.EventHandlers.ImageGenerationFailedHandler>();
            services.AddEventHandler<VideoGenerationProgress, Gateway.EventHandlers.VideoGenerationProgressHandler>();
            services.AddEventHandler<VideoGenerationCompleted, Gateway.EventHandlers.VideoGenerationCompletedHandler>();
            services.AddEventHandler<VideoGenerationFailed, Gateway.EventHandlers.VideoGenerationFailedHandler>();

            return services;
        }

        /// <summary>
        /// Registers the MassTransit bridge consumers for the media-generation events.
        /// </summary>
        public static void AddMediaGenerationBridges(this IRegistrationConfigurator x)
        {
            x.AddEventBridge<ImageGenerationRequested>();
            x.AddEventBridge<ImageGenerationCancelled>();
            x.AddEventBridge<VideoGenerationRequested>();
            x.AddEventBridge<VideoGenerationCancelled>();
            x.AddEventBridge<VideoProgressCheckRequested>();
            x.AddEventBridge<ImageGenerationProgress>();
            x.AddEventBridge<ImageGenerationCompleted>();
            x.AddEventBridge<ImageGenerationFailed>();
            x.AddEventBridge<VideoGenerationProgress>();
            x.AddEventBridge<VideoGenerationCompleted>();
            x.AddEventBridge<VideoGenerationFailed>();
        }
    }
}
