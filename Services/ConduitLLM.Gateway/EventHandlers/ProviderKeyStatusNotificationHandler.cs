using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers;

/// <summary>
/// Persists provider-key status notifications and broadcasts them to connected admins.
/// </summary>
public sealed class ProviderKeyStatusNotificationHandler :
    IEventHandler<ProviderKeyDisabledEvent>,
    IEventHandler<ProviderKeyReenabledEvent>
{
    private const int MaxNotificationLength = 500;
    private const int MaxProviderErrorLength = 240;

    private readonly IProviderRepository _providerRepository;
    private readonly IProviderKeyCredentialRepository _keyRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly ISystemNotificationService _notificationService;
    private readonly ILogger<ProviderKeyStatusNotificationHandler> _logger;

    public ProviderKeyStatusNotificationHandler(
        IProviderRepository providerRepository,
        IProviderKeyCredentialRepository keyRepository,
        INotificationRepository notificationRepository,
        ISystemNotificationService notificationService,
        ILogger<ProviderKeyStatusNotificationHandler> logger)
    {
        _providerRepository = providerRepository;
        _keyRepository = keyRepository;
        _notificationRepository = notificationRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(ProviderKeyDisabledEvent @event, IEventContext context)
    {
        var (providerName, keyName, providerExists, keyExists) =
            await ResolveNamesAsync(@event.ProviderId, @event.KeyId);
        var origin = @event.IsAutomatic ? "automatically" : "manually";
        var errorType = string.IsNullOrWhiteSpace(@event.ErrorType)
            ? "Unknown"
            : @event.ErrorType;
        var providerError = ToSingleLine(
            string.IsNullOrWhiteSpace(@event.ErrorMessage) ? @event.Reason : @event.ErrorMessage,
            MaxProviderErrorLength);
        var message = Truncate(
            $"Provider \"{providerName}\" key \"{keyName}\" (ID {@event.KeyId}) was {origin} disabled. " +
            $"Error type: {errorType}. Provider error: {providerError}",
            MaxNotificationLength);

        await PersistAsync(
            NotificationType.ProviderKeyDisabled,
            NotificationSeverity.Error,
            message,
            @event.ProviderId,
            @event.KeyId,
            providerExists,
            keyExists,
            context.CancellationToken);
        await BroadcastBestEffortAsync(message, NotificationSeverity.Error);
    }

    public async Task HandleAsync(ProviderKeyReenabledEvent @event, IEventContext context)
    {
        var (providerName, keyName, providerExists, keyExists) =
            await ResolveNamesAsync(@event.ProviderId, @event.KeyId);
        var reason = ToSingleLine(@event.Reason, MaxProviderErrorLength);
        var message = Truncate(
            $"Provider \"{providerName}\" key \"{keyName}\" (ID {@event.KeyId}) was re-enabled " +
            $"by {@event.ReenabledBy}. Reason: {reason}",
            MaxNotificationLength);

        await PersistAsync(
            NotificationType.ProviderKeyReenabled,
            NotificationSeverity.Info,
            message,
            @event.ProviderId,
            @event.KeyId,
            providerExists,
            keyExists,
            context.CancellationToken);
        await BroadcastBestEffortAsync(message, NotificationSeverity.Info);
    }

    private async Task<(string ProviderName, string KeyName, bool ProviderExists, bool KeyExists)>
        ResolveNamesAsync(int providerId, int keyId)
    {
        var providerTask = _providerRepository.GetByIdAsync(providerId);
        var keyTask = _keyRepository.GetByIdAsync(keyId);
        await Task.WhenAll(providerTask, keyTask);

        var provider = await providerTask;
        var key = await keyTask;

        return (
            provider?.ProviderName ?? $"Provider {providerId}",
            key?.KeyName ?? $"Key {keyId}",
            provider != null,
            key != null);
    }

    private async Task PersistAsync(
        NotificationType type,
        NotificationSeverity severity,
        string message,
        int providerId,
        int keyId,
        bool providerExists,
        bool keyExists,
        CancellationToken cancellationToken)
    {
        await _notificationRepository.CreateAsync(new Notification
        {
            ProviderId = providerExists ? providerId : null,
            ProviderKeyCredentialId = keyExists ? keyId : null,
            Type = type,
            Severity = severity,
            Message = message,
            IsRead = false
        }, cancellationToken);
    }

    private async Task BroadcastBestEffortAsync(string message, NotificationSeverity severity)
    {
        try
        {
            await _notificationService.NotifySystemAnnouncement(message, severity);
        }
        catch (Exception ex)
        {
            // The durable notification has already been saved. Do not redeliver the
            // transport message and create duplicates solely because a live client failed.
            _logger.LogWarning(ex, "Failed to broadcast provider key status notification");
        }
    }

    private static string ToSingleLine(string? value, int maxLength)
    {
        var singleLine = string.IsNullOrWhiteSpace(value)
            ? "No provider error text was supplied."
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return Truncate(singleLine, maxLength);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
