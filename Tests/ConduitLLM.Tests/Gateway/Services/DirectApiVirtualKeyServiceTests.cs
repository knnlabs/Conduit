using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Gateway.Services;

public class DirectApiVirtualKeyServiceTests
{
    private readonly Mock<IVirtualKeyRepository> _virtualKeyRepository = new();
    private readonly Mock<IVirtualKeyGroupRepository> _groupRepository = new();
    private readonly Mock<IVirtualKeySpendHistoryRepository> _spendHistoryRepository = new();
    private readonly Mock<IBatchSpendUpdateService> _batchSpendService = new();
    private readonly DirectApiVirtualKeyService _service;

    public DirectApiVirtualKeyServiceTests()
    {
        _service = new DirectApiVirtualKeyService(
            _virtualKeyRepository.Object,
            _groupRepository.Object,
            _spendHistoryRepository.Object,
            null,
            Mock.Of<ILogger<DirectApiVirtualKeyService>>(),
            _batchSpendService.Object);
    }

    [Fact]
    public async Task ValidateVirtualKeyAsync_WhenGroupPendingSpendConsumesBalance_ReturnsTypedFailure()
    {
        const string keyValue = "condt_pending_spend";
        var virtualKey = new VirtualKey
        {
            Id = 17,
            VirtualKeyGroupId = 42,
            IsEnabled = true
        };
        var group = new VirtualKeyGroup
        {
            Id = virtualKey.VirtualKeyGroupId,
            Balance = 5m
        };

        _virtualKeyRepository
            .Setup(repository => repository.GetByKeyHashAsync(
                ConduitLLM.Configuration.Utilities.VirtualKeyUtilities.HashKey(keyValue)))
            .ReturnsAsync(virtualKey);
        _groupRepository.Setup(repository => repository.GetByIdAsync(group.Id))
            .ReturnsAsync(group);
        _batchSpendService.Setup(service => service.GetPendingSpendAsync(virtualKey.Id))
            .ReturnsAsync(group.Balance);

        var result = await _service.ValidateVirtualKeyAsync(keyValue);

        Assert.False(result.IsValid);
        Assert.Equal(VirtualKeyValidationFailureCodes.InsufficientBalance, result.FailureCode);
        Assert.Equal(402, result.HttpStatusCode);
        Assert.Same(virtualKey, result.Key);
        _batchSpendService.Verify(
            service => service.GetPendingSpendAsync(virtualKey.Id),
            Times.Once);
    }

    [Fact]
    public async Task ValidateVirtualKeyForAuthenticationAsync_DoesNotCheckBalance()
    {
        const string keyValue = "condt_auth_only";
        var virtualKey = new VirtualKey
        {
            Id = 18,
            VirtualKeyGroupId = 43,
            IsEnabled = true
        };
        _virtualKeyRepository
            .Setup(repository => repository.GetByKeyHashAsync(
                ConduitLLM.Configuration.Utilities.VirtualKeyUtilities.HashKey(keyValue)))
            .ReturnsAsync(virtualKey);

        var result = await _service.ValidateVirtualKeyForAuthenticationAsync(keyValue);

        Assert.True(result.IsValid);
        Assert.Same(virtualKey, result.Key);
        _groupRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<int>()),
            Times.Never);
        _batchSpendService.Verify(
            service => service.GetPendingSpendAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("expired")]
    [InlineData("restricted")]
    [InlineData("insufficient_balance")]
    [InlineData("valid")]
    public async Task CachedAndDirectServices_ReturnEquivalentValidationOutcomes(string scenario)
    {
        const string keyValue = "condt_parity";
        var keyHash = ConduitLLM.Configuration.Utilities.VirtualKeyUtilities.HashKey(keyValue);
        var virtualKey = scenario == "missing"
            ? null
            : new VirtualKey
            {
                Id = 23,
                VirtualKeyGroupId = 9,
                IsEnabled = scenario != "disabled",
                ExpiresAt = scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : null,
                AllowedModels = scenario == "restricted" ? "gpt-4" : null
            };
        var group = new VirtualKeyGroup
        {
            Id = 9,
            Balance = scenario == "insufficient_balance" ? 0m : 10m
        };
        var cache = new Mock<IVirtualKeyCache>();
        cache
            .Setup(value => value.GetVirtualKeyAsync(
                keyHash,
                It.IsAny<Func<string, Task<VirtualKey>>>() ))
            .ReturnsAsync(virtualKey);
        _virtualKeyRepository
            .Setup(repository => repository.GetByKeyHashAsync(keyHash))
            .ReturnsAsync(virtualKey);
        _groupRepository
            .Setup(repository => repository.GetByIdAsync(group.Id))
            .ReturnsAsync(group);

        var cachedService = new CachedApiVirtualKeyService(
            _virtualKeyRepository.Object,
            _spendHistoryRepository.Object,
            _groupRepository.Object,
            cache.Object,
            null,
            Mock.Of<ILogger<CachedApiVirtualKeyService>>(),
            _batchSpendService.Object);
        var requestedModel = scenario == "restricted" ? "claude-3-opus" : null;

        var directResult = await _service.ValidateVirtualKeyAsync(keyValue, requestedModel);
        var cachedResult = await cachedService.ValidateVirtualKeyAsync(keyValue, requestedModel);

        Assert.Equal(directResult.IsValid, cachedResult.IsValid);
        Assert.Equal(directResult.FailureCode, cachedResult.FailureCode);
        Assert.Equal(directResult.HttpStatusCode, cachedResult.HttpStatusCode);
        Assert.Equal(directResult.Reason, cachedResult.Reason);
        Assert.Equal(directResult.Key?.Id, cachedResult.Key?.Id);
    }

    [Fact]
    public async Task ResetSpendAsync_ZeroesLifetimeSpendWithoutCreditingBalance()
    {
        var virtualKey = new VirtualKey { Id = 7, VirtualKeyGroupId = 3 };
        var group = new VirtualKeyGroup
        {
            Id = 3,
            Balance = 25m,
            LifetimeCreditsAdded = 100m,
            LifetimeSpent = 40m
        };

        _virtualKeyRepository.Setup(repository => repository.GetByIdAsync(virtualKey.Id))
            .ReturnsAsync(virtualKey);
        _virtualKeyRepository.Setup(repository => repository.UpdateAsync(virtualKey))
            .ReturnsAsync(true);
        _groupRepository.Setup(repository => repository.GetByIdAsync(group.Id))
            .ReturnsAsync(group);
        _groupRepository.Setup(repository => repository.UpdateAsync(group))
            .ReturnsAsync(true);

        var result = await _service.ResetSpendAsync(virtualKey.Id);

        Assert.True(result);
        Assert.Equal(0m, group.LifetimeSpent);
        Assert.Equal(25m, group.Balance);
        Assert.Equal(100m, group.LifetimeCreditsAdded);
        _groupRepository.Verify(repository => repository.AdjustBalanceAsync(
            It.IsAny<int>(),
            It.IsAny<decimal>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<ReferenceType>(),
            It.IsAny<string?>()), Times.Never);
        _groupRepository.Verify(repository => repository.UpdateAsync(group), Times.Once);
        _spendHistoryRepository.Verify(repository => repository.CreateAsync(
            It.Is<VirtualKeySpendHistory>(history =>
                history.VirtualKeyId == virtualKey.Id && history.Amount == 40m)), Times.Once);
    }
}
