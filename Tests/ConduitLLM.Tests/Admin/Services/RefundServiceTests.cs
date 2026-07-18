using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

public class RefundServiceTests
{
    private readonly Mock<ICostCalculationService> _mockCostCalculationService;
    private readonly Mock<IVirtualKeyGroupRepository> _mockGroupRepository;
    private readonly Mock<IConfigurationDbContext> _mockContext;
    private readonly Mock<ILogger<RefundService>> _mockLogger;
    private readonly RefundService _service;

    public RefundServiceTests()
    {
        _mockCostCalculationService = new Mock<ICostCalculationService>();
        _mockGroupRepository = new Mock<IVirtualKeyGroupRepository>();
        _mockContext = new Mock<IConfigurationDbContext>();
        _mockLogger = new Mock<ILogger<RefundService>>();

        _service = new RefundService(
            _mockCostCalculationService.Object,
            _mockGroupRepository.Object,
            _mockContext.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessRefundAsync_WithValidData_ShouldUpdateBalanceAndReturnResult()
    {
        // Arrange
        var groupId = 1;
        var modelId = "gpt-4";
        var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
        var refundUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
        var group = new VirtualKeyGroup { Id = groupId, Balance = 50.00m, UpdatedAt = DateTime.UtcNow };

        var refundResult = new RefundResult
        {
            ModelId = modelId,
            RefundAmount = 0.15m,
            RefundReason = "Incorrect response",
            ValidationMessages = new List<string>()
        };

        _mockGroupRepository.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _mockCostCalculationService.Setup(x => x.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Incorrect response", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResult);
        _mockContext.Setup(x => x.VirtualKeyGroups).Returns(Mock.Of<Microsoft.EntityFrameworkCore.DbSet<VirtualKeyGroup>>());
        _mockContext.Setup(x => x.VirtualKeyGroupTransactions).Returns(Mock.Of<Microsoft.EntityFrameworkCore.DbSet<VirtualKeyGroupTransaction>>());
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _service.ProcessRefundAsync(
            groupId, modelId, originalUsage, refundUsage,
            "Incorrect response", null, "admin", null);

        // Assert
        result.Should().NotBeNull();
        result.RefundAmount.Should().Be(0.15m);
        group.Balance.Should().Be(50.15m);
    }

    [Fact]
    public async Task ProcessRefundAsync_WithNonExistentGroup_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockGroupRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VirtualKeyGroup?)null);

        // Act
        var act = () => _service.ProcessRefundAsync(
            999, "gpt-4",
            new Usage { PromptTokens = 100, TotalTokens = 100 },
            new Usage { PromptTokens = 100, TotalTokens = 100 },
            "reason", null, "admin", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*999*not found*");
    }

    [Fact]
    public async Task ProcessRefundAsync_WithValidationErrors_ShouldThrowArgumentException()
    {
        // Arrange
        var group = new VirtualKeyGroup { Id = 1, Balance = 50.00m };
        var refundResult = new RefundResult
        {
            RefundAmount = 0,
            ValidationMessages = new List<string> { "Model not found in cost configuration" }
        };

        _mockGroupRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _mockCostCalculationService.Setup(x => x.CalculateRefundAsync(
                It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<Usage>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResult);

        // Act
        var act = () => _service.ProcessRefundAsync(
            1, "unknown-model",
            new Usage { PromptTokens = 100, TotalTokens = 100 },
            new Usage { PromptTokens = 100, TotalTokens = 100 },
            "reason", null, "admin", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public async Task ProcessRefundAsync_WithValidationWarningsButNonZeroRefund_ShouldSucceed()
    {
        // Arrange
        var group = new VirtualKeyGroup { Id = 1, Balance = 10.00m, UpdatedAt = DateTime.UtcNow };
        var refundResult = new RefundResult
        {
            ModelId = "gpt-4",
            RefundAmount = 0.05m,
            RefundReason = "partial",
            ValidationMessages = new List<string> { "Partial refund: output tokens capped" }
        };

        _mockGroupRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _mockCostCalculationService.Setup(x => x.CalculateRefundAsync(
                It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<Usage>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResult);
        _mockContext.Setup(x => x.VirtualKeyGroups).Returns(Mock.Of<Microsoft.EntityFrameworkCore.DbSet<VirtualKeyGroup>>());
        _mockContext.Setup(x => x.VirtualKeyGroupTransactions).Returns(Mock.Of<Microsoft.EntityFrameworkCore.DbSet<VirtualKeyGroupTransaction>>());
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _service.ProcessRefundAsync(
            1, "gpt-4",
            new Usage { PromptTokens = 1000, TotalTokens = 1000 },
            new Usage { PromptTokens = 500, TotalTokens = 500 },
            "partial", null, "admin", null);

        // Assert
        result.Should().NotBeNull();
        result.RefundAmount.Should().Be(0.05m);
        group.Balance.Should().Be(10.05m);
    }
}
