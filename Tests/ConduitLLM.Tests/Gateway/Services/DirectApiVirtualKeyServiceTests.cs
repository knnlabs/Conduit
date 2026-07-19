using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Gateway.Services;

public class DirectApiVirtualKeyServiceTests
{
    private readonly Mock<IVirtualKeyRepository> _virtualKeyRepository = new();
    private readonly Mock<IVirtualKeyGroupRepository> _groupRepository = new();
    private readonly Mock<IVirtualKeySpendHistoryRepository> _spendHistoryRepository = new();
    private readonly DirectApiVirtualKeyService _service;

    public DirectApiVirtualKeyServiceTests()
    {
        _service = new DirectApiVirtualKeyService(
            _virtualKeyRepository.Object,
            _groupRepository.Object,
            _spendHistoryRepository.Object,
            null,
            Mock.Of<ILogger<DirectApiVirtualKeyService>>());
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
