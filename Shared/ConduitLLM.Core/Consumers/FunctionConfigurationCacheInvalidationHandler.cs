using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Consumers;

/// <summary>
/// Consumer that handles FunctionConfigurationChanged events to invalidate function discovery cache
/// across all Gateway API instances in a distributed deployment.
///
/// This ensures cache consistency when function configurations are modified via the Admin API.
/// </summary>
public class FunctionConfigurationCacheInvalidationHandler : ConduitLLM.Configuration.Messaging.IEventHandler<FunctionConfigurationChanged>
{
    private readonly IFunctionDiscoveryCacheService _cacheService;
    private readonly ILogger<FunctionConfigurationCacheInvalidationHandler> _logger;

    public FunctionConfigurationCacheInvalidationHandler(
        IFunctionDiscoveryCacheService cacheService,
        ILogger<FunctionConfigurationCacheInvalidationHandler> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(FunctionConfigurationChanged message, IEventContext context)
    {
        _logger.LogInformation(
            "Received FunctionConfigurationChanged event for '{ConfigName}' (ID: {ConfigId}, Provider: {ProviderType}, ChangeType: {ChangeType})",
            message.ConfigurationName,
            message.FunctionConfigurationId,
            message.ProviderType,
            message.ChangeType);

        try
        {
            // Invalidate all function discovery cache entries
            // Since we cache by lists of IDs, invalidating all is the safest approach
            await _cacheService.InvalidateAllFunctionDiscoveryAsync();

            _logger.LogInformation(
                "Successfully invalidated function discovery cache for '{ConfigName}' (ID: {ConfigId})",
                message.ConfigurationName,
                message.FunctionConfigurationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to invalidate function discovery cache for '{ConfigName}' (ID: {ConfigId})",
                message.ConfigurationName,
                message.FunctionConfigurationId);

            // Rethrow to allow the transport retry policy to handle the failure
            throw;
        }
    }
}

/// <summary>
/// Consumer that handles FunctionDiscoveryCacheInvalidationRequested events for manual cache invalidation
/// triggered by admins via the Admin API.
/// </summary>
public class FunctionDiscoveryCacheInvalidationRequestHandler : ConduitLLM.Configuration.Messaging.IEventHandler<FunctionDiscoveryCacheInvalidationRequested>
{
    private readonly IFunctionDiscoveryCacheService _cacheService;
    private readonly ILogger<FunctionDiscoveryCacheInvalidationRequestHandler> _logger;

    public FunctionDiscoveryCacheInvalidationRequestHandler(
        IFunctionDiscoveryCacheService cacheService,
        ILogger<FunctionDiscoveryCacheInvalidationRequestHandler> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(FunctionDiscoveryCacheInvalidationRequested message, IEventContext context)
    {
        _logger.LogInformation(
            "Received FunctionDiscoveryCacheInvalidationRequested event. Reason: {Reason}, Requested by: {RequestedBy}",
            message.Reason,
            message.RequestedBy);

        try
        {
            await _cacheService.InvalidateAllFunctionDiscoveryAsync();

            _logger.LogInformation(
                "Successfully invalidated all function discovery cache entries. Reason: {Reason}",
                message.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to invalidate function discovery cache. Reason: {Reason}",
                message.Reason);

            // Rethrow to allow the transport retry policy to handle the failure
            throw;
        }
    }
}
