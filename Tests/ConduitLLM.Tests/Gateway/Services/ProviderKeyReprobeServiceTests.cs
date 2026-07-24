using System.Net;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Options;
using ConduitLLM.Gateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Gateway.Services;

public class ProviderKeyReprobeServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 8, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRedisErrorStore> _errorStore = new();
    private readonly Mock<IProviderKeyCredentialRepository> _keyRepository = new();
    private readonly Mock<IProviderRepository> _providerRepository = new();
    private readonly Mock<ILLMClientFactory> _clientFactory = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<ILLMClient> _client = new();
    private readonly ProviderKeyCredential _key = new()
    {
        Id = 7,
        ProviderId = 42,
        KeyName = "Primary",
        IsEnabled = false
    };
    private readonly Provider _provider = new()
    {
        Id = 42,
        ProviderName = "OpenAI",
        IsEnabled = false
    };

    [Fact]
    public async Task RunOnceAsync_SuccessReenablesKeyAndAutoDisabledProvider()
    {
        var service = CreateService();
        _client
            .Setup(x => x.ListModelsAsync(
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "model" });
        _errorStore
            .Setup(x => x.GetProviderSummaryAsync(42))
            .ReturnsAsync(new ProviderSummaryData
            {
                ProviderDisabledAt = Now.UtcDateTime.AddHours(-2),
                ProviderDisableReason = ProviderErrorTrackingService.AllKeysDisabledReason
            });

        await service.RunOnceAsync();

        _keyRepository.Verify(x => x.UpdateAsync(
            It.Is<ProviderKeyCredential>(key => key.Id == 7 && key.IsEnabled),
            It.IsAny<CancellationToken>()), Times.Once);
        _providerRepository.Verify(x => x.UpdateAsync(
            It.Is<Provider>(provider => provider.Id == 42 && provider.IsEnabled),
            It.IsAny<CancellationToken>()), Times.Once);
        _errorStore.Verify(x => x.ClearErrorsForKeyAsync(7, 42), Times.Once);
        _eventBus.Verify(x => x.PublishAsync(
            It.Is<ProviderKeyReenabledEvent>(message =>
                message.KeyId == 7 &&
                message.ReenabledBy == "auto-reprobe"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_ContinuedBalanceFailureSchedulesExponentialBackoff()
    {
        var service = CreateService();
        _client
            .Setup(x => x.ListModelsAsync(
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LLMCommunicationException(
                "payment required",
                HttpStatusCode.PaymentRequired,
                "insufficient balance"));

        await service.RunOnceAsync();

        _errorStore.Verify(x => x.RecordKeyReprobeAttemptAsync(
            7,
            1,
            Now.UtcDateTime,
            Now.UtcDateTime.AddHours(2)), Times.Once);
        _keyRepository.Verify(x => x.UpdateAsync(
            It.IsAny<ProviderKeyCredential>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_UnauthorizedProbeRequiresManualRepair()
    {
        var service = CreateService();
        _client
            .Setup(x => x.ListModelsAsync(
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LLMCommunicationException(
                "unauthorized",
                HttpStatusCode.Unauthorized,
                "invalid key"));

        await service.RunOnceAsync();

        _errorStore.Verify(x => x.MarkKeyReprobeRequiresManualAsync(
            7, ProviderErrorType.InvalidApiKey), Times.Once);
        _errorStore.Verify(x => x.RecordKeyReprobeAttemptAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>()), Times.Never);
    }

    private ProviderKeyReprobeService CreateService()
    {
        _errorStore
            .Setup(x => x.GetDisabledKeyReprobeStatesAsync())
            .ReturnsAsync(new List<DisabledKeyReprobeState>
            {
                new()
                {
                    KeyId = 7,
                    ErrorType = ProviderErrorType.InsufficientBalance,
                    DisabledAt = Now.UtcDateTime.AddHours(-2)
                }
            });
        _errorStore
            .Setup(x => x.TryAcquireKeyReprobeAsync(7, It.IsAny<TimeSpan>()))
            .ReturnsAsync(true);
        _keyRepository.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(_key);
        _providerRepository
            .Setup(x => x.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_provider);
        _clientFactory
            .Setup(x => x.CreateTestClient(_provider, _key))
            .Returns(_client.Object);

        var services = new ServiceCollection();
        services.AddScoped(_ => _keyRepository.Object);
        services.AddScoped(_ => _providerRepository.Object);
        services.AddScoped(_ => _clientFactory.Object);
        services.AddScoped(_ => _eventBus.Object);
        var serviceProvider = services.BuildServiceProvider();

        return new ProviderKeyReprobeService(
            _errorStore.Object,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProviderKeyReprobeOptions()),
            Mock.Of<ILogger<ProviderKeyReprobeService>>(),
            new FixedTimeProvider(Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
