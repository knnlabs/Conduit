using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Consumers;

/// <summary>
/// Bridges a durable reload request onto Redis pub/sub so every process-local cache
/// reloads, including sibling instances that compete on the same Wolverine queue.
/// </summary>
public sealed class GlobalSettingsReloadHandler : IEventHandler<GlobalSettingsReloadRequested>
{
    private readonly IGlobalSettingsCacheService _cacheService;
    private readonly ILogger<GlobalSettingsReloadHandler> _logger;

    public GlobalSettingsReloadHandler(
        IGlobalSettingsCacheService cacheService,
        ILogger<GlobalSettingsReloadHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(GlobalSettingsReloadRequested message, IEventContext context)
    {
        _logger.LogInformation(
            "Broadcasting global settings reload request {RequestId}",
            message.CorrelationId);
        await _cacheService.PublishReloadAsync(message.CorrelationId);
    }
}
