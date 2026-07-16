using ConduitLLM.Configuration.Messaging.MassTransit;
using ConduitLLM.Core.Events;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Registration for the low-risk cache-invalidation / notification handlers migrated to
    /// <c>IEventHandler&lt;T&gt;</c> in epic #909 (issue #919). Centralizes the handler DI
    /// registrations and the matching MassTransit bridge consumers so the Gateway and Admin
    /// hosts stay in sync. These handlers are idempotent and dispatched on the default
    /// (auto-configured) endpoints, exactly as their <c>IConsumer&lt;T&gt;</c> predecessors were.
    /// </summary>
    public static class CacheInvalidationMessagingExtensions
    {
        /// <summary>
        /// Registers the Gateway-hosted cache-invalidation/notification handlers and their
        /// bridges (the handlers that run only in the Gateway process).
        /// </summary>
        public static IServiceCollection AddGatewayCacheInvalidationHandlers(this IServiceCollection services)
        {
            // VirtualKey cache invalidation (one class, four event types)
            services.AddEventHandler<VirtualKeyUpdated, Gateway.EventHandlers.VirtualKeyCacheInvalidationHandler>();
            services.AddEventHandler<VirtualKeyCreated, Gateway.EventHandlers.VirtualKeyCacheInvalidationHandler>();
            services.AddEventHandler<VirtualKeyDeleted, Gateway.EventHandlers.VirtualKeyCacheInvalidationHandler>();
            services.AddEventHandler<SpendUpdated, Gateway.EventHandlers.VirtualKeyCacheInvalidationHandler>();

            // Spend notification (SignalR)
            services.AddEventHandler<SpendUpdated, Gateway.EventHandlers.SpendUpdatedHandler>();

            // Provider events: capability refresh + cache invalidation (two classes)
            services.AddEventHandler<ProviderUpdated, Gateway.EventHandlers.ProviderEventHandler>();
            services.AddEventHandler<ProviderDeleted, Gateway.EventHandlers.ProviderEventHandler>();
            services.AddEventHandler<ProviderCreated, Gateway.EventHandlers.ProviderCacheInvalidationHandler>();
            services.AddEventHandler<ProviderUpdated, Gateway.EventHandlers.ProviderCacheInvalidationHandler>();
            services.AddEventHandler<ProviderDeleted, Gateway.EventHandlers.ProviderCacheInvalidationHandler>();

            // Model / discovery / async-task / media / video-started caches
            services.AddEventHandler<ModelUpdated, Gateway.EventHandlers.ModelCacheInvalidationHandler>();
            services.AddEventHandler<DiscoveryCacheInvalidationRequested, Gateway.EventHandlers.DiscoveryCacheInvalidationHandler>();
            services.AddEventHandler<AsyncTaskCreated, Gateway.EventHandlers.AsyncTaskCacheInvalidationHandler>();
            services.AddEventHandler<AsyncTaskUpdated, Gateway.EventHandlers.AsyncTaskCacheInvalidationHandler>();
            services.AddEventHandler<AsyncTaskDeleted, Gateway.EventHandlers.AsyncTaskCacheInvalidationHandler>();
            services.AddEventHandler<MediaGenerationCompleted, Gateway.EventHandlers.MediaLifecycleHandler>();
            services.AddEventHandler<VideoGenerationStarted, Gateway.EventHandlers.VideoGenerationStartedHandler>();

            // Model-mapping / model-cost / ip-filter / provider-tool caches
            services.AddEventHandler<ModelMappingChanged, Gateway.Consumers.ModelMappingCacheInvalidationHandler>();
            services.AddEventHandler<ModelCostChanged, Gateway.Consumers.ModelCostCacheInvalidationHandler>();
            services.AddEventHandler<IpFilterChanged, Gateway.Consumers.IpFilterCacheInvalidationHandler>();
            services.AddEventHandler<ProviderToolChanged, Gateway.Consumers.ProviderToolCacheInvalidationHandler>();

            // Provider key-credential cache invalidation (one class, four event types).
            // These events live in ConduitLLM.Configuration.Events (that is what the Admin
            // publish sites emit), unlike the rest of this file which uses Core.Events.
            services.AddEventHandler<Configuration.Events.ProviderKeyCredentialCreated, Gateway.EventHandlers.ProviderKeyCredentialCacheInvalidationHandler>();
            services.AddEventHandler<Configuration.Events.ProviderKeyCredentialUpdated, Gateway.EventHandlers.ProviderKeyCredentialCacheInvalidationHandler>();
            services.AddEventHandler<Configuration.Events.ProviderKeyCredentialDeleted, Gateway.EventHandlers.ProviderKeyCredentialCacheInvalidationHandler>();
            services.AddEventHandler<Configuration.Events.ProviderKeyCredentialPrimaryChanged, Gateway.EventHandlers.ProviderKeyCredentialCacheInvalidationHandler>();

            return services;
        }

        /// <summary>
        /// Registers the MassTransit bridge consumers for the Gateway-hosted cache events.
        /// </summary>
        public static void AddGatewayCacheInvalidationBridges(this IRegistrationConfigurator x)
        {
            x.AddEventBridge<VirtualKeyUpdated>();
            x.AddEventBridge<VirtualKeyCreated>();
            x.AddEventBridge<VirtualKeyDeleted>();
            x.AddEventBridge<SpendUpdated>();
            x.AddEventBridge<ProviderCreated>();
            x.AddEventBridge<ProviderUpdated>();
            x.AddEventBridge<ProviderDeleted>();
            x.AddEventBridge<ModelUpdated>();
            x.AddEventBridge<DiscoveryCacheInvalidationRequested>();
            x.AddEventBridge<AsyncTaskCreated>();
            x.AddEventBridge<AsyncTaskUpdated>();
            x.AddEventBridge<AsyncTaskDeleted>();
            x.AddEventBridge<MediaGenerationCompleted>();
            x.AddEventBridge<VideoGenerationStarted>();
            x.AddEventBridge<ModelMappingChanged>();
            x.AddEventBridge<ModelCostChanged>();
            x.AddEventBridge<IpFilterChanged>();
            x.AddEventBridge<ProviderToolChanged>();
            x.AddEventBridge<Configuration.Events.ProviderKeyCredentialCreated>();
            x.AddEventBridge<Configuration.Events.ProviderKeyCredentialUpdated>();
            x.AddEventBridge<Configuration.Events.ProviderKeyCredentialDeleted>();
            x.AddEventBridge<Configuration.Events.ProviderKeyCredentialPrimaryChanged>();
        }
    }
}
