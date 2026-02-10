using ConduitLLM.Core.Consumers;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles DiscoveryCacheInvalidationRequested events from Admin API
    /// Invalidates the discovery cache across all Gateway API instances
    /// </summary>
    public class DiscoveryCacheInvalidationHandler : CacheInvalidationConsumerBase<DiscoveryCacheInvalidationRequested>
    {
        private readonly IDiscoveryCacheService _discoveryCacheService;

        public DiscoveryCacheInvalidationHandler(
            IDiscoveryCacheService discoveryCacheService,
            ILogger<DiscoveryCacheInvalidationHandler> logger)
            : base(logger)
        {
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
        }

        protected override Task InvalidateCacheAsync(DiscoveryCacheInvalidationRequested message)
            => _discoveryCacheService.InvalidateAllDiscoveryAsync();

        protected override void LogReceived(DiscoveryCacheInvalidationRequested message)
            => Logger.LogInformation(
                "Processing discovery cache invalidation request. Reason: {Reason}, Requested by: {RequestedBy}",
                message.Reason,
                message.RequestedBy);

        protected override void LogSuccess(DiscoveryCacheInvalidationRequested message)
            => Logger.LogInformation(
                "Successfully invalidated all discovery cache entries. Reason: {Reason}",
                message.Reason);

        protected override void LogFailure(DiscoveryCacheInvalidationRequested message, Exception ex)
            => Logger.LogError(ex,
                "Failed to invalidate discovery cache. Reason: {Reason}",
                message.Reason);
    }
}
