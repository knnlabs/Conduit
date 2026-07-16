using ConduitLLM.Configuration.Messaging.MassTransit;
using ConduitLLM.Core.Consumers;
using ConduitLLM.Core.Events;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Registration for the cache-invalidation handlers that run in BOTH the Gateway and
    /// Admin hosts (defined in <see cref="ConduitLLM.Core.Consumers"/>), migrated to
    /// <c>IEventHandler&lt;T&gt;</c> in epic #909 (issue #919). Lives in Core because both
    /// hosts reference Core (Admin does not reference Gateway).
    /// </summary>
    public static class SharedCacheInvalidationMessagingExtensions
    {
        /// <summary>Registers the shared Core cache-invalidation handlers.</summary>
        public static IServiceCollection AddSharedCacheInvalidationHandlers(this IServiceCollection services)
        {
            services.AddEventHandler<GlobalSettingChanged, GlobalSettingCacheInvalidationHandler>();
            services.AddEventHandler<FunctionConfigurationChanged, FunctionConfigurationCacheInvalidationHandler>();
            services.AddEventHandler<FunctionDiscoveryCacheInvalidationRequested, FunctionDiscoveryCacheInvalidationRequestHandler>();
            return services;
        }

        /// <summary>Registers the MassTransit bridge consumers for the shared Core cache events.</summary>
        public static void AddSharedCacheInvalidationBridges(this IRegistrationConfigurator x)
        {
            x.AddEventBridge<GlobalSettingChanged>();
            x.AddEventBridge<FunctionConfigurationChanged>();
            x.AddEventBridge<FunctionDiscoveryCacheInvalidationRequested>();
        }
    }
}
