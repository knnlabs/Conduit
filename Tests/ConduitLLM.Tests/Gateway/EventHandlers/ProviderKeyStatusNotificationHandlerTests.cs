using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.EventHandlers;
using ConduitLLM.Tests.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Gateway.EventHandlers;

public class ProviderKeyStatusNotificationHandlerTests
{
    private readonly Mock<IProviderRepository> _providerRepository = new();
    private readonly Mock<IProviderKeyCredentialRepository> _keyRepository = new();
    private readonly Mock<INotificationRepository> _notificationRepository = new();
    private readonly Mock<ISystemNotificationService> _notificationService = new();
    private readonly ProviderKeyStatusNotificationHandler _handler;

    public ProviderKeyStatusNotificationHandlerTests()
    {
        _providerRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Provider { Id = 42, ProviderName = "OpenAI" });
        _keyRepository
            .Setup(x => x.GetByIdAsync(7))
            .ReturnsAsync(new ProviderKeyCredential { Id = 7, ProviderId = 42, KeyName = "Primary" });

        _handler = new ProviderKeyStatusNotificationHandler(
            _providerRepository.Object,
            _keyRepository.Object,
            _notificationRepository.Object,
            _notificationService.Object,
            Mock.Of<ILogger<ProviderKeyStatusNotificationHandler>>());
    }

    [Fact]
    public async Task DisabledEvent_PersistsActionableNotificationAndBroadcasts()
    {
        await _handler.HandleAsync(new ProviderKeyDisabledEvent
        {
            ProviderId = 42,
            KeyId = 7,
            ErrorType = "InvalidApiKey",
            ErrorMessage = "401 invalid credential",
            IsAutomatic = true
        }, new TestEventContext());

        _notificationRepository.Verify(x => x.CreateAsync(
            It.Is<Notification>(notification =>
                notification.Type == NotificationType.ProviderKeyDisabled &&
                notification.Severity == NotificationSeverity.Error &&
                notification.ProviderId == 42 &&
                notification.ProviderKeyCredentialId == 7 &&
                notification.Message.Contains("OpenAI") &&
                notification.Message.Contains("Primary") &&
                notification.Message.Contains("InvalidApiKey") &&
                notification.Message.Contains("401 invalid credential") &&
                notification.Message.Contains("automatically")),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationService.Verify(x => x.NotifySystemAnnouncement(
            It.Is<string>(message => message.Contains("InvalidApiKey")),
            NotificationSeverity.Error), Times.Once);
    }

    [Fact]
    public async Task ReenabledEvent_PersistsAndBroadcastsRecoveryNotification()
    {
        await _handler.HandleAsync(new ProviderKeyReenabledEvent
        {
            ProviderId = 42,
            KeyId = 7,
            ReenabledBy = "admin@example.com",
            Reason = "Credential rotated"
        }, new TestEventContext());

        _notificationRepository.Verify(x => x.CreateAsync(
            It.Is<Notification>(notification =>
                notification.Type == NotificationType.ProviderKeyReenabled &&
                notification.Severity == NotificationSeverity.Info &&
                notification.ProviderId == 42 &&
                notification.ProviderKeyCredentialId == 7 &&
                notification.Message.Contains("admin@example.com") &&
                notification.Message.Contains("Credential rotated")),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationService.Verify(x => x.NotifySystemAnnouncement(
            It.IsAny<string>(),
            NotificationSeverity.Info), Times.Once);
    }

    [Fact]
    public async Task LiveBroadcastFailure_DoesNotFailAfterDurablePersistence()
    {
        _notificationService
            .Setup(x => x.NotifySystemAnnouncement(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new InvalidOperationException("no live connections"));

        await _handler.HandleAsync(new ProviderKeyDisabledEvent
        {
            ProviderId = 42,
            KeyId = 7,
            ErrorType = "AccessForbidden",
            ErrorMessage = "forbidden"
        }, new TestEventContext());

        _notificationRepository.Verify(x => x.CreateAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
