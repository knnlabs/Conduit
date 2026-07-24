using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Endpoints;

public class ProviderErrorsEndpointsTests
{
    private readonly Mock<IProviderErrorTrackingService> _errorService = new();
    private readonly Mock<IProviderKeyCredentialRepository> _keyRepository = new();
    private readonly Mock<IProviderRepository> _providerRepository = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly ProviderErrorsEndpoints _endpoints;

    public ProviderErrorsEndpointsTests()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        _endpoints = new ProviderErrorsEndpoints(
            _errorService.Object,
            _keyRepository.Object,
            _providerRepository.Object,
            _eventPublisher.Object,
            httpContextAccessor,
            Mock.Of<ILogger<ProviderErrorsEndpoints>>());
    }

    [Fact]
    public async Task ClearKeyErrors_ReenablesProviderWhenAllKeysAutoDisabledIt()
    {
        const int keyId = 11;
        const int providerId = 22;
        var key = new ProviderKeyCredential
        {
            Id = keyId,
            ProviderId = providerId,
            IsEnabled = false
        };
        var provider = new Provider
        {
            Id = providerId,
            IsEnabled = false
        };

        _keyRepository.Setup(x => x.GetByIdAsync(keyId)).ReturnsAsync(key);
        _providerRepository
            .Setup(x => x.GetByIdAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);
        _errorService
            .Setup(x => x.GetProviderSummaryAsync(providerId))
            .ReturnsAsync(new ProviderErrorSummary
            {
                ProviderId = providerId,
                ProviderDisabledAt = DateTime.UtcNow,
                ProviderDisableReason = ProviderErrorTrackingService.AllKeysDisabledReason
            });

        await _endpoints.ClearKeyErrors(keyId, new ClearErrorsRequest
        {
            ReenableKey = true,
            ConfirmReenable = true
        });

        _keyRepository.Verify(
            x => x.UpdateAsync(
                It.Is<ProviderKeyCredential>(candidate => candidate.Id == keyId && candidate.IsEnabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _providerRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Provider>(candidate => candidate.Id == providerId && candidate.IsEnabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _errorService.Verify(x => x.ClearProviderDisabledAsync(providerId), Times.Once);
    }

    [Fact]
    public async Task ClearKeyErrors_DoesNotOverrideManualProviderDisable()
    {
        const int keyId = 11;
        const int providerId = 22;
        var key = new ProviderKeyCredential
        {
            Id = keyId,
            ProviderId = providerId,
            IsEnabled = false
        };

        _keyRepository.Setup(x => x.GetByIdAsync(keyId)).ReturnsAsync(key);
        _errorService
            .Setup(x => x.GetProviderSummaryAsync(providerId))
            .ReturnsAsync(new ProviderErrorSummary
            {
                ProviderId = providerId,
                ProviderDisabledAt = null,
                ProviderDisableReason = null
            });

        await _endpoints.ClearKeyErrors(keyId, new ClearErrorsRequest
        {
            ReenableKey = true,
            ConfirmReenable = true
        });

        _providerRepository.Verify(
            x => x.UpdateAsync(It.IsAny<Provider>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _errorService.Verify(
            x => x.ClearProviderDisabledAsync(It.IsAny<int>()),
            Times.Never);
    }
}
