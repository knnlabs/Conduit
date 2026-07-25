using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Security;
using ConduitLLM.Core.Interfaces;

using FluentAssertions;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

/// <summary>
/// Secret settings are write-only: the API accepts them, stores them encrypted, and never returns a
/// value - only the names of the settings a credential has configured (issue #1187).
/// </summary>
public class ProviderKeySecretSettingsEndpointTests
{
    private const string SecretValue = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private readonly Mock<IProviderRepository> _providerRepository = new();
    private readonly Mock<IProviderKeyCredentialRepository> _keyRepository = new();
    private readonly ProviderSecretProtector _protector = new(
        DataProtectionProvider.Create(nameof(ProviderKeySecretSettingsEndpointTests)),
        NullLogger<ProviderSecretProtector>.Instance);

    private ProviderCredentialsEndpoints CreateEndpoints()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext());

        return new ProviderCredentialsEndpoints(
            _providerRepository.Object,
            _keyRepository.Object,
            Mock.Of<ILLMClientFactory>(),
            _protector,
            Mock.Of<IEventBus>(),
            accessor.Object,
            NullLogger<ProviderCredentialsEndpoints>.Instance);
    }

    [Fact]
    public async Task CreateKey_Should_Store_Secret_Settings_Encrypted_And_Never_Return_Their_Values()
    {
        _providerRepository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Provider { Id = 1, ProviderType = ProviderType.OpenAI, ProviderName = "openai" });

        ProviderKeyCredential? persisted = null;
        _keyRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<ProviderKeyCredential>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderKeyCredential, CancellationToken>((key, _) => persisted = key)
            .ReturnsAsync(9);

        var result = await CreateEndpoints().CreateProviderKeyCredential(1, new CreateKeyRequest
        {
            ApiKey = "sk-test",
            KeyName = "primary",
            SecretSettings = new Dictionary<string, string> { ["secret_access_key"] = SecretValue }
        });

        // Stored encrypted, and recoverable.
        persisted.Should().NotBeNull();
        var stored = persisted!.SecretSettings.Should().ContainKey("secret_access_key").WhoseValue;
        stored.Should().NotBe(SecretValue);
        stored.Should().StartWith(ProviderSecretProtector.Prefix);
        _protector.Reveal(stored).Should().Be(SecretValue);

        // Returned to the caller as a name only - never the value, in any form.
        var dto = ExtractKeyDto(result);
        dto.ConfiguredSecretSettings.Should().Equal("secret_access_key");
        System.Text.Json.JsonSerializer.Serialize(dto).Should().NotContain(SecretValue);
    }

    [Fact]
    public async Task UpdateKey_Should_Replace_Secret_Settings_Wholesale_And_Keep_Them_Encrypted()
    {
        var existing = new ProviderKeyCredential
        {
            Id = 9,
            ProviderId = 1,
            ApiKey = "sk-test",
            SecretSettings = _protector.ProtectAll(new Dictionary<string, string> { ["old_key"] = "old-value" })
        };
        _keyRepository
            .Setup(repository => repository.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _keyRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<ProviderKeyCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateEndpoints().UpdateProviderKeyCredential(1, 9, new UpdateKeyRequest
        {
            SecretSettings = new Dictionary<string, string> { ["secret_access_key"] = SecretValue }
        });

        existing.SecretSettings.Should().ContainKey("secret_access_key");
        existing.SecretSettings.Should().NotContainKey("old_key");
        _protector.Reveal(existing.SecretSettings!["secret_access_key"]).Should().Be(SecretValue);

        var dto = ExtractKeyDto(result);
        dto.ConfiguredSecretSettings.Should().Equal("secret_access_key");
        System.Text.Json.JsonSerializer.Serialize(dto).Should().NotContain(SecretValue);
    }

    [Fact]
    public async Task UpdateKey_Should_Leave_Stored_Secrets_Untouched_When_None_Are_Supplied()
    {
        var protectedSettings = _protector.ProtectAll(new Dictionary<string, string> { ["secret_access_key"] = SecretValue });
        var existing = new ProviderKeyCredential
        {
            Id = 9,
            ProviderId = 1,
            ApiKey = "sk-test",
            SecretSettings = protectedSettings
        };
        _keyRepository
            .Setup(repository => repository.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _keyRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<ProviderKeyCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateEndpoints().UpdateProviderKeyCredential(1, 9, new UpdateKeyRequest { KeyName = "renamed" });

        existing.SecretSettings.Should().BeEquivalentTo(protectedSettings);
    }

    private static ProviderKeyCredentialDto ExtractKeyDto(IResult result) => result switch
    {
        Created<ProviderKeyCredentialDto> created => created.Value!,
        Ok<ProviderKeyCredentialDto> ok => ok.Value!,
        _ => throw new InvalidOperationException($"Unexpected result type {result.GetType().Name}")
    };
}
