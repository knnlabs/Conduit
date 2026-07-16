using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Consumers;

/// <summary>
/// Handles ProviderToolChanged events for cache invalidation.
/// Invalidates the provider tool cache when tools are created, updated, or deleted.
/// </summary>
public class ProviderToolCacheInvalidationHandler : IEventHandler<ProviderToolChanged>
{
    private readonly IProviderToolCache? _providerToolCache;
    private readonly ILogger<ProviderToolCacheInvalidationHandler> _logger;

    public ProviderToolCacheInvalidationHandler(
        IProviderToolCache? providerToolCache,
        ILogger<ProviderToolCacheInvalidationHandler> logger)
    {
        _providerToolCache = providerToolCache;
        _logger = logger;
    }

    public async Task HandleAsync(ProviderToolChanged message, IEventContext context)
    {
        var @event = message;

        _logger.LogInformation(
            "ProviderToolChanged event received - ToolId: {ToolId}, ToolName: {ToolName}, Provider: {Provider}, ChangeType: {ChangeType}",
            @event.ProviderToolId,
            @event.ToolName,
            @event.ProviderType,
            @event.ChangeType);

        if (_providerToolCache == null)
        {
            _logger.LogDebug("Provider tool cache not available, skipping invalidation");
            return;
        }

        try
        {
            if (Enum.TryParse<ProviderType>(@event.ProviderType, true, out var providerType))
            {
                await _providerToolCache.InvalidateProviderAsync(providerType);
                _logger.LogInformation("Provider tool cache invalidated for {ProviderType} due to {ChangeType} event",
                    providerType, @event.ChangeType);
            }
            else
            {
                // Unknown provider type — clear all to be safe
                await _providerToolCache.ClearAllAsync();
                _logger.LogWarning("Unknown provider type '{ProviderType}' in event, cleared all provider tool cache entries",
                    @event.ProviderType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating provider tool cache for {ProviderType}", @event.ProviderType);
        }
    }
}
