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
/// Provider credential secrets are write-only: the API accepts them, stores them encrypted, and
/// returns only a masked API key plus the names of configured structured settings (issues #1187 and
/// #1242).
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
    public async Task CreateKey_Should_Store_All_Secrets_Encrypted_And_Never_Return_Their_Values()
    {
        _providerRepository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Provider { Id = 1, ProviderType = ProviderType.OpenAI, ProviderName = "openai" });

        ProviderKeyCredential? persisted = null;
        _keyRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<ProviderKeyCredential>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderKeyCredential, CancellationToken>((key, _) => persisted = key)
            .ReturnsAsync(9);
        _keyRepository
            .Setup(repository => repository.GetByProviderIdPaginatedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProviderKeyCredential>(), 0));

        var result = await CreateEndpoints().CreateProviderKeyCredential(1, new CreateKeyRequest
        {
            ApiKey = "sk-test",
            KeyName = "primary",
            SecretSettings = new Dictionary<string, string> { ["secret_access_key"] = SecretValue }
        });

        // Stored encrypted, and recoverable.
        persisted.Should().NotBeNull();
        persisted!.ApiKey.Should().NotBe("sk-test");
        persisted.ApiKey.Should().StartWith(ProviderSecretProtector.Prefix);
        _protector.Reveal(persisted.ApiKey).Should().Be("sk-test");

        var stored = persisted!.SecretSettings.Should().ContainKey("secret_access_key").WhoseValue;
        stored.Should().NotBe(SecretValue);
        stored.Should().StartWith(ProviderSecretProtector.Prefix);
        _protector.Reveal(stored).Should().Be(SecretValue);

        // Returned to the caller as a name only - never the value, in any form.
        var dto = ExtractKeyDto(result);
        dto.ApiKey.Should().Be("***test");
        dto.ConfiguredSecretSettings.Should().Equal("secret_access_key");
        System.Text.Json.JsonSerializer.Serialize(dto).Should().NotContain(SecretValue);
    }

    [Fact]
    public async Task CreateKey_Should_Return409_When_ApiKey_Already_Exists_Encrypted()
    {
        _providerRepository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Provider { Id = 1, ProviderType = ProviderType.OpenAI, ProviderName = "openai" });
        // Data Protection ciphertext differs on every Protect call, so this only matches if the
        // endpoint compares revealed values, not stored ones.
        _keyRepository
            .Setup(repository => repository.GetByProviderIdPaginatedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProviderKeyCredential>
            {
                new() { Id = 5, ProviderId = 1, ApiKey = _protector.Protect("sk-test") }
            }, 1));

        var result = await CreateEndpoints().CreateProviderKeyCredential(1, new CreateKeyRequest
        {
            ApiKey = "sk-test",
            KeyName = "duplicate"
        });

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status409Conflict);
        _keyRepository.Verify(
            repository => repository.CreateAsync(It.IsAny<ProviderKeyCredential>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateKey_Should_Return409_When_ApiKey_Matches_Legacy_Plaintext_Row()
    {
        _providerRepository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Provider { Id = 1, ProviderType = ProviderType.OpenAI, ProviderName = "openai" });
        _keyRepository
            .Setup(repository => repository.GetByProviderIdPaginatedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProviderKeyCredential>
            {
                new() { Id = 5, ProviderId = 1, ApiKey = "sk-test" }
            }, 1));

        var result = await CreateEndpoints().CreateProviderKeyCredential(1, new CreateKeyRequest
        {
            ApiKey = "sk-test",
            KeyName = "duplicate"
        });

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CreateKey_Should_Succeed_When_ApiKey_Differs_From_Existing_Keys()
    {
        _providerRepository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Provider { Id = 1, ProviderType = ProviderType.OpenAI, ProviderName = "openai" });
        _keyRepository
            .Setup(repository => repository.GetByProviderIdPaginatedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProviderKeyCredential>
            {
                new() { Id = 5, ProviderId = 1, ApiKey = _protector.Protect("sk-other") }
            }, 1));
        _keyRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<ProviderKeyCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(9);

        var result = await CreateEndpoints().CreateProviderKeyCredential(1, new CreateKeyRequest
        {
            ApiKey = "sk-test",
            KeyName = "unique"
        });

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status201Created);
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
        existing.ApiKey.Should().StartWith(ProviderSecretProtector.Prefix);
        _protector.Reveal(existing.ApiKey).Should().Be("sk-test");
    }

    [Fact]
    public async Task UpdateKey_Should_Encrypt_A_Replacement_Api_Key_And_Mask_Its_Plaintext_Suffix()
    {
        var existing = new ProviderKeyCredential
        {
            Id = 9,
            ProviderId = 1,
            ApiKey = _protector.Protect("old-key")
        };
        _keyRepository
            .Setup(repository => repository.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _keyRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<ProviderKeyCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateEndpoints().UpdateProviderKeyCredential(
            1,
            9,
            new UpdateKeyRequest { ApiKey = "sk-replacement" });

        existing.ApiKey.Should().NotBe("sk-replacement");
        existing.ApiKey.Should().StartWith(ProviderSecretProtector.Prefix);
        _protector.Reveal(existing.ApiKey).Should().Be("sk-replacement");
        ExtractKeyDto(result).ApiKey.Should().Be("***ment");
    }

    [Fact]
    public async Task GetKey_Should_Keep_Legacy_Plaintext_Keys_Readable_And_Mask_Their_Suffix()
    {
        _keyRepository
            .Setup(repository => repository.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderKeyCredential
            {
                Id = 9,
                ProviderId = 1,
                ApiKey = "legacy-key-1234"
            });

        var result = await CreateEndpoints().GetProviderKeyCredential(1, 9);

        ExtractKeyDto(result).ApiKey.Should().Be("***1234");
    }

    private static ProviderKeyCredentialDto ExtractKeyDto(IResult result) => result switch
    {
        Created<ProviderKeyCredentialDto> created => created.Value!,
        Ok<ProviderKeyCredentialDto> ok => ok.Value!,
        _ => throw new InvalidOperationException($"Unexpected result type {result.GetType().Name}")
    };
}
