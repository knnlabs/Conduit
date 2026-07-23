using ConduitLLM.Configuration;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

public class RefundServiceTests : IDisposable
{
    private readonly Mock<ICostCalculationService> _mockCostCalculationService;
    private readonly Mock<IVirtualKeyGroupRepository> _mockGroupRepository;
    private readonly ConduitDbContext _context;
    private readonly Mock<ILogger<RefundService>> _mockLogger;
    private readonly RefundService _service;
    private readonly SqliteTestDatabase _database;

    public RefundServiceTests()
    {
        _mockCostCalculationService = new Mock<ICostCalculationService>();
        _mockGroupRepository = new Mock<IVirtualKeyGroupRepository>();
        _database = new SqliteTestDatabase();
        _context = _database.CreateContext();
        _mockLogger = new Mock<ILogger<RefundService>>();

        _service = new RefundService(
            _mockCostCalculationService.Object,
            _mockGroupRepository.Object,
            _context,
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
        _context.VirtualKeyGroups.Add(group);
        await _context.SaveChangesAsync();
        var originalTransactionId = await AddDebitAsync(groupId, 1m);
        _mockCostCalculationService.Setup(x => x.CalculateRefundAsync(
                modelId, originalUsage, refundUsage, "Incorrect response", originalTransactionId,
                It.Is<ProviderCostRefundContext?>(c => c != null && c.OriginalChargedCost == 1m),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResult);
        // Act
        var result = await _service.ProcessRefundAsync(
            groupId, modelId, originalUsage, refundUsage,
            "Incorrect response", originalTransactionId, "test-refund", "admin", null);

        // Assert
        result.Should().NotBeNull();
        result.RefundAmount.Should().Be(0.15m);
        (await _context.VirtualKeyGroups.AsNoTracking().SingleAsync()).Balance.Should().Be(50.15m);
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
            "reason", "1", "test-refund", "admin", null);

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
        _context.VirtualKeyGroups.Add(group);
        await _context.SaveChangesAsync();
        var originalTransactionId = await AddDebitAsync(1, 1m);
        _mockCostCalculationService.Setup(x => x.CalculateRefundAsync(
                It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<Usage>(),
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<ProviderCostRefundContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResult);

        // Act
        var act = () => _service.ProcessRefundAsync(
            1, "unknown-model",
            new Usage { PromptTokens = 100, TotalTokens = 100 },
            new Usage { PromptTokens = 100, TotalTokens = 100 },
            "reason", originalTransactionId, "test-refund", "admin", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public async Task ProcessRefundAsync_WithValidationErrorsAndNonZeroRefund_ShouldRejectWithoutCreditingBalance()
    {
        // Arrange
        var group = new VirtualKeyGroup { Id = 1, Balance = 10.00m, UpdatedAt = DateTime.UtcNow };
        var refundResult = new RefundResult
        {
            ModelId = "gpt-4",
            RefundAmount = 0.05m,
            RefundReason = "partial",
            ValidationMessages = new List<string> { "Refund prompt tokens cannot exceed original" }
        };

        _mockGroupRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _context.VirtualKeyGroups.Add(group);
        await _context.SaveChangesAsync();
        var originalTransactionId = await AddDebitAsync(1, 1m);
        _mockCostCalculationService.Setup(x => x.CalculateRefundAsync(
                It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<Usage>(),
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<ProviderCostRefundContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResult);
        // Act
        var act = () => _service.ProcessRefundAsync(
            1, "gpt-4",
            new Usage { PromptTokens = 1000, TotalTokens = 1000 },
            new Usage { PromptTokens = 1500, TotalTokens = 1500 },
            "partial", originalTransactionId, "test-refund", "admin", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Refund prompt tokens cannot exceed original*");
        group.Balance.Should().Be(10.00m);
        (await _context.VirtualKeyGroupTransactions.CountAsync(
            t => t.TransactionType == TransactionType.Refund)).Should().Be(0);
    }

    [Fact]
    public async Task ProcessRefundAsync_WithoutOriginalTransactionId_ShouldRejectRequest()
    {
        var act = () => _service.ProcessRefundAsync(
            1, "gpt-4", new Usage(), new Usage(), "reason", null!, "test-refund", "admin", null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*original debit transaction ID is required*");
    }

    [Fact]
    public async Task ProcessRefundAsync_WithUnknownOriginalTransaction_ShouldRejectBeforeCalculation()
    {
        var group = new VirtualKeyGroup { Id = 1, Balance = 10m };
        _mockGroupRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var act = () => _service.ProcessRefundAsync(
            1, "gpt-4", new Usage(), new Usage(), "reason", "999", "test-refund", "admin", null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*transaction 999 was not found*");
        _mockCostCalculationService.Verify(x => x.CalculateRefundAsync(
            It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<Usage>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<ProviderCostRefundContext?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<string> AddDebitAsync(int groupId, decimal amount)
    {
        var transaction = new VirtualKeyGroupTransaction
        {
            VirtualKeyGroupId = groupId,
            TransactionType = TransactionType.Debit,
            Amount = amount,
            BalanceAfter = 0,
            ReferenceType = ReferenceType.System
        };
        _context.VirtualKeyGroupTransactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction.Id.ToString();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }
}
