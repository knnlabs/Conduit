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
public class FunctionConfigurationCacheInvalidationHandler : CacheInvalidationConsumerBase<FunctionConfigurationChanged>
{
    private readonly IFunctionDiscoveryCacheService _cacheService;

    public FunctionConfigurationCacheInvalidationHandler(
        IFunctionDiscoveryCacheService cacheService,
        ILogger<FunctionConfigurationCacheInvalidationHandler> logger)
        : base(logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    protected override Task InvalidateCacheAsync(FunctionConfigurationChanged message)
        => _cacheService.InvalidateAllFunctionDiscoveryAsync();

    protected override void LogReceived(FunctionConfigurationChanged message)
        => Logger.LogInformation(
            "Received FunctionConfigurationChanged event for '{ConfigName}' (ID: {ConfigId}, Provider: {ProviderType}, ChangeType: {ChangeType})",
            message.ConfigurationName,
            message.FunctionConfigurationId,
            message.ProviderType,
            message.ChangeType);

    protected override void LogSuccess(FunctionConfigurationChanged message)
        => Logger.LogInformation(
            "Successfully invalidated function discovery cache for '{ConfigName}' (ID: {ConfigId})",
            message.ConfigurationName,
            message.FunctionConfigurationId);

    protected override void LogFailure(FunctionConfigurationChanged message, Exception ex)
        => Logger.LogError(
            ex,
            "Failed to invalidate function discovery cache for '{ConfigName}' (ID: {ConfigId})",
            message.ConfigurationName,
            message.FunctionConfigurationId);
}

/// <summary>
/// Consumer that handles FunctionDiscoveryCacheInvalidationRequested events for manual cache invalidation
/// triggered by admins via the Admin API.
/// </summary>
public class FunctionDiscoveryCacheInvalidationRequestHandler : CacheInvalidationConsumerBase<FunctionDiscoveryCacheInvalidationRequested>
{
    private readonly IFunctionDiscoveryCacheService _cacheService;

    public FunctionDiscoveryCacheInvalidationRequestHandler(
        IFunctionDiscoveryCacheService cacheService,
        ILogger<FunctionDiscoveryCacheInvalidationRequestHandler> logger)
        : base(logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    protected override Task InvalidateCacheAsync(FunctionDiscoveryCacheInvalidationRequested message)
        => _cacheService.InvalidateAllFunctionDiscoveryAsync();

    protected override void LogReceived(FunctionDiscoveryCacheInvalidationRequested message)
        => Logger.LogInformation(
            "Received FunctionDiscoveryCacheInvalidationRequested event. Reason: {Reason}, Requested by: {RequestedBy}",
            message.Reason,
            message.RequestedBy);

    protected override void LogSuccess(FunctionDiscoveryCacheInvalidationRequested message)
        => Logger.LogInformation(
            "Successfully invalidated all function discovery cache entries. Reason: {Reason}",
            message.Reason);

    protected override void LogFailure(FunctionDiscoveryCacheInvalidationRequested message, Exception ex)
        => Logger.LogError(
            ex,
            "Failed to invalidate function discovery cache. Reason: {Reason}",
            message.Reason);
}
