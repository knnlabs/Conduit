using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Security;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

/// <summary>
/// Covers Test Connection reporting a misconfigured provider as an actionable result rather than
/// failing the request outright, which callers could only surface as an unexpected error.
/// </summary>
public class ProviderConnectionTestEndpointTests
{
    private const string MissingSettingMessage =
        "Cloudflare is missing required configuration: Account ID. Provide the value in the provider settings.";

    private readonly Mock<IProviderRepository> _providerRepository = new();
    private readonly Mock<IProviderKeyCredentialRepository> _keyRepository = new();
    private readonly Mock<ILLMClientFactory> _clientFactory = new();

    private ProviderCredentialsEndpoints CreateEndpoints()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext());

        return new ProviderCredentialsEndpoints(
            _providerRepository.Object,
            _keyRepository.Object,
            _clientFactory.Object,
            Mock.Of<IProviderSecretProtector>(),
            Mock.Of<IEventBus>(),
            accessor.Object,
            NullLogger<ProviderCredentialsEndpoints>.Instance);
    }

    private static Provider CloudflareWithoutAccountId() => new()
    {
        Id = 7,
        ProviderType = ProviderType.Cloudflare,
        ProviderName = "cf"
    };

    [Fact]
    public async Task TestProviderConnection_Should_Report_A_Missing_Required_Setting_As_Configuration()
    {
        _providerRepository
            .Setup(repository => repository.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CloudflareWithoutAccountId());
        _clientFactory
            .Setup(factory => factory.GetClientByProviderIdAsync(7, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConfigurationException(MissingSettingMessage));

        var result = await CreateEndpoints().TestProviderConnection(7);

        var response = result.Should().BeOfType<Ok<StandardApiKeyTestResponse>>().Subject.Value!;
        response.Result.Should().Be(ApiKeyTestResult.Configuration);
        response.Message.Should().Be(MissingSettingMessage);
    }

    [Fact]
    public async Task TestProviderKeyCredential_Should_Report_A_Missing_Required_Setting_As_Configuration()
    {
        var key = new ProviderKeyCredential { Id = 3, ProviderId = 7, ApiKey = "token", IsPrimary = true };
        _keyRepository
            .Setup(repository => repository.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);
        _providerRepository
            .Setup(repository => repository.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CloudflareWithoutAccountId());
        _clientFactory
            .Setup(factory => factory.CreateTestClient(It.IsAny<Provider>(), It.IsAny<ProviderKeyCredential>()))
            .Throws(new ConfigurationException(MissingSettingMessage));

        var result = await CreateEndpoints().TestProviderKeyCredential(7, 3);

        var response = result.Should().BeOfType<Ok<StandardApiKeyTestResponse>>().Subject.Value!;
        response.Result.Should().Be(ApiKeyTestResult.Configuration);
        response.Message.Should().Be(MissingSettingMessage);
    }
}
