using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for refund service to ensure
    /// refunds are processed correctly with proper balance updates and transaction records
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Component", "RefundService")]
    public class RefundServiceIntegrationTests : IDisposable
    {
        private readonly IConfigurationDbContext _dbContext;
        private readonly ConduitDbContext _concreteDbContext;
        private readonly VirtualKeyGroupRepository _groupRepository;
        private readonly IRefundService _refundService;
        private readonly Mock<ICostCalculationService> _mockCostCalculationService;
        private readonly Mock<ILogger<VirtualKeyGroupRepository>> _mockGroupLogger;
        private readonly Mock<ILogger<RefundService>> _mockRefundLogger;
        private readonly DbContextOptions<ConduitDbContext> _dbOptions;

        public RefundServiceIntegrationTests()
        {
            // Setup in-memory database for integration testing
            _dbOptions = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _concreteDbContext = new ConduitDbContext(_dbOptions);
            _dbContext = _concreteDbContext;

            // Create a mock factory that returns contexts with the same database
            var mockFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConduitDbContext(_dbOptions));

            _mockGroupLogger = new Mock<ILogger<VirtualKeyGroupRepository>>();
            _groupRepository = new VirtualKeyGroupRepository(mockFactory.Object, _mockGroupLogger.Object);

            _mockCostCalculationService = new Mock<ICostCalculationService>();
            _mockRefundLogger = new Mock<ILogger<RefundService>>();

            _refundService = new RefundService(
                _mockCostCalculationService.Object,
                _groupRepository,
                _dbContext,
                _mockRefundLogger.Object);
        }

        [Fact]
        public async Task ProcessRefund_WithValidRequest_ShouldCreateRefundTransactionAndUpdateBalance()
        {
            // Arrange
            var initialBalance = 100m;
            var refundAmount = 15.50m;
            var expectedBalance = initialBalance + refundAmount;

            var group = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 0
            };

            var groupId = await _groupRepository.CreateAsync(group);
            var originalTransactionId = await AddDebitAsync(groupId, 50m, initialBalance);

            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundReason = "Service interruption";

            // Mock the cost calculation service to return a refund result
            var mockRefundResult = new RefundResult
            {
                ModelId = modelId,
                OriginalUsage = originalUsage,
                RefundUsage = refundUsage,
                RefundAmount = refundAmount,
                RefundReason = refundReason,
                RefundedAt = DateTime.UtcNow,
                IsPartialRefund = false,
                ValidationMessages = new List<string>(),
                Breakdown = new RefundBreakdown
                {
                    InputTokenRefund = 5.00m,
                    OutputTokenRefund = 10.50m
                }
            };

            _mockCostCalculationService
                .Setup(s => s.CalculateRefundAsync(
                    modelId,
                    originalUsage,
                    refundUsage,
                    refundReason,
                    It.IsAny<string?>(),
                    It.IsAny<ProviderCostRefundContext?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRefundResult);

            // Act
            var result = await _refundService.ProcessRefundAsync(
                groupId,
                modelId,
                originalUsage,
                refundUsage,
                refundReason,
                originalTransactionId,
                "valid-refund",
                "TestAdmin",
                "test-user-123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(refundAmount, result.RefundAmount);
            Assert.Equal(modelId, result.ModelId);
            Assert.False(result.IsPartialRefund);
            Assert.Empty(result.ValidationMessages);

            // Verify the group balance was updated correctly
            var updatedGroup = await _groupRepository.GetByIdAsync(groupId);
            Assert.NotNull(updatedGroup);
            Assert.Equal(expectedBalance, updatedGroup.Balance);

            // Verify transaction was created with correct values
            var transactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId && t.TransactionType == TransactionType.Refund)
                .ToListAsync();

            Assert.Single(transactions);
            var transaction = transactions.First();

            Assert.Equal(TransactionType.Refund, transaction.TransactionType);
            Assert.Equal(refundAmount, transaction.Amount); // Should be positive
            Assert.Equal(expectedBalance, transaction.BalanceAfter); // Should match new balance
            Assert.Contains("Refund:", transaction.Description);
            Assert.Contains(refundReason, transaction.Description);
            Assert.Equal("TestAdmin", transaction.InitiatedBy);
            Assert.Equal("test-user-123", transaction.InitiatedByUserId);
            Assert.Equal(ReferenceType.Manual, transaction.ReferenceType);
        }

        [Fact]
        public async Task ProcessRefund_WithInvalidGroup_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var invalidGroupId = 99999;
            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250 };
            var refundReason = "Service interruption";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _refundService.ProcessRefundAsync(
                    invalidGroupId,
                    modelId,
                    originalUsage,
                    refundUsage,
                    refundReason,
                    "1",
                    "invalid-group-refund",
                    "TestAdmin",
                    null);
            });
        }

        [Fact]
        public async Task ProcessRefund_WithValidationErrors_ShouldThrowArgumentException()
        {
            // Arrange
            var initialBalance = 100m;
            var group = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = initialBalance
            };

            var groupId = await _groupRepository.CreateAsync(group);
            var originalTransactionId = await AddDebitAsync(groupId, 50m, initialBalance);

            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500 };
            var refundUsage = new Usage { PromptTokens = 2000, CompletionTokens = 1000 }; // More than original
            var refundReason = "Service interruption";

            // Mock the cost calculation service to return validation errors
            var mockRefundResult = new RefundResult
            {
                ModelId = modelId,
                OriginalUsage = originalUsage,
                RefundUsage = refundUsage,
                RefundAmount = 0, // No refund due to validation errors
                RefundReason = refundReason,
                RefundedAt = DateTime.UtcNow,
                IsPartialRefund = false,
                ValidationMessages = new List<string> { "Refund tokens cannot exceed original tokens" }
            };

            _mockCostCalculationService
                .Setup(s => s.CalculateRefundAsync(
                    modelId,
                    originalUsage,
                    refundUsage,
                    refundReason,
                    It.IsAny<string?>(),
                    It.IsAny<ProviderCostRefundContext?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRefundResult);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _refundService.ProcessRefundAsync(
                    groupId,
                    modelId,
                    originalUsage,
                    refundUsage,
                    refundReason,
                    originalTransactionId,
                    "validation-refund",
                    "TestAdmin",
                    null);
            });
        }

        [Fact]
        public async Task ProcessRefund_WithDuplicateOperation_ShouldReplayAndNotDoubleCredit()
        {
            // Arrange
            var initialBalance = 100m;
            var refundAmount = 15.50m;
            var group = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 0
            };
            var groupId = await _groupRepository.CreateAsync(group);

            var modelId = "openai/gpt-4o";
            var originalUsage = new Usage { PromptTokens = 1000, CompletionTokens = 500, TotalTokens = 1500 };
            var refundUsage = new Usage { PromptTokens = 500, CompletionTokens = 250, TotalTokens = 750 };
            var refundReason = "Service interruption";
            var originalTransactionId = await AddDebitAsync(groupId, 50m, initialBalance);

            _mockCostCalculationService
                .Setup(s => s.CalculateRefundAsync(
                    modelId, originalUsage, refundUsage, refundReason,
                    It.IsAny<string?>(), It.IsAny<ProviderCostRefundContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new RefundResult
                {
                    ModelId = modelId,
                    OriginalUsage = originalUsage,
                    RefundUsage = refundUsage,
                    RefundAmount = refundAmount,
                    RefundReason = refundReason,
                    OriginalTransactionId = originalTransactionId,
                    ValidationMessages = new List<string>()
                });

            // Act - first refund succeeds
            var firstResult = await _refundService.ProcessRefundAsync(
                groupId, modelId, originalUsage, refundUsage, refundReason,
                originalTransactionId, idempotencyKey: "refund-operation-1",
                initiatedBy: "TestAdmin", initiatedByUserId: null);

            // The same operation is a successful replay, not a second credit.
            var replayResult = await _refundService.ProcessRefundAsync(
                groupId, modelId, originalUsage, refundUsage, refundReason,
                originalTransactionId, idempotencyKey: "refund-operation-1",
                initiatedBy: "TestAdmin", initiatedByUserId: null);

            await Assert.ThrowsAsync<ConduitLLM.Configuration.Exceptions.IdempotencyConflictException>(() =>
                _refundService.ProcessRefundAsync(
                    groupId, modelId, originalUsage, refundUsage, "changed reason",
                    originalTransactionId, idempotencyKey: "refund-operation-1",
                    initiatedBy: "TestAdmin", initiatedByUserId: null));

            // Assert - balance credited exactly once, linkage preserved, single refund transaction
            Assert.Equal(refundAmount, firstResult.RefundAmount);
            Assert.Equal(firstResult.RefundTransactionId, replayResult.RefundTransactionId);
            Assert.Equal(firstResult.BalanceAfter, replayResult.BalanceAfter);
            Assert.Equal(originalTransactionId, firstResult.OriginalTransactionId); // not overwritten
            Assert.NotEqual(0, firstResult.RefundTransactionId);

            var updatedGroup = await _groupRepository.GetByIdAsync(groupId);
            Assert.NotNull(updatedGroup);
            Assert.Equal(initialBalance + refundAmount, updatedGroup!.Balance);

            var refundTransactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId && t.TransactionType == TransactionType.Refund)
                .ToListAsync();
            Assert.Single(refundTransactions);
            Assert.Equal(originalTransactionId, refundTransactions[0].ReferenceId);
        }

        [Fact]
        public async Task ProcessRefund_WithCumulativeRefundAboveOriginalDebit_ShouldRejectExcess()
        {
            var initialBalance = 100m;
            var group = new VirtualKeyGroup
            {
                GroupName = "Cumulative Refund Group",
                Balance = initialBalance
            };
            var groupId = await _groupRepository.CreateAsync(group);
            var originalTransactionId = await AddDebitAsync(groupId, 10m, initialBalance);
            var originalUsage = new Usage { PromptTokens = 100, TotalTokens = 100 };
            var firstUsage = new Usage { PromptTokens = 60, TotalTokens = 60 };
            var secondUsage = new Usage { PromptTokens = 50, TotalTokens = 50 };

            _mockCostCalculationService
                .Setup(s => s.CalculateRefundAsync(
                    It.IsAny<string>(), originalUsage, firstUsage, It.IsAny<string>(),
                    originalTransactionId, It.IsAny<ProviderCostRefundContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefundResult { RefundAmount = 6m, ValidationMessages = [] });
            _mockCostCalculationService
                .Setup(s => s.CalculateRefundAsync(
                    It.IsAny<string>(), originalUsage, secondUsage, It.IsAny<string>(),
                    originalTransactionId, It.IsAny<ProviderCostRefundContext?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefundResult { RefundAmount = 5m, ValidationMessages = [] });

            await _refundService.ProcessRefundAsync(
                groupId, "openai/gpt-4o", originalUsage, firstUsage, "first partial",
                originalTransactionId, "first-partial-refund", "TestAdmin", null);

            var act = () => _refundService.ProcessRefundAsync(
                groupId, "openai/gpt-4o", originalUsage, secondUsage, "second partial",
                originalTransactionId, "second-partial-refund", "TestAdmin", null);

            await Assert.ThrowsAsync<InvalidOperationException>(act);
            var updatedGroup = await _groupRepository.GetByIdAsync(groupId);
            Assert.Equal(initialBalance + 6m, updatedGroup!.Balance);
            Assert.Single(await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.TransactionType == TransactionType.Refund)
                .ToListAsync());
        }

        [Fact]
        public async Task ProcessRefund_WithNonDebitOriginalTransaction_ShouldRejectRequest()
        {
            var group = new VirtualKeyGroup { GroupName = "Invalid Original", Balance = 100m };
            var groupId = await _groupRepository.CreateAsync(group);
            var credit = new VirtualKeyGroupTransaction
            {
                VirtualKeyGroupId = groupId,
                TransactionType = TransactionType.Credit,
                Amount = 10m,
                BalanceAfter = 100m,
                ReferenceType = ReferenceType.Manual
            };
            _dbContext.VirtualKeyGroupTransactions.Add(credit);
            await _dbContext.SaveChangesAsync();

            var act = () => _refundService.ProcessRefundAsync(
                groupId, "openai/gpt-4o", new Usage(), new Usage(), "invalid original",
                credit.Id.ToString(), "invalid-original-refund", "TestAdmin", null);

            await Assert.ThrowsAsync<ArgumentException>(act);
            _mockCostCalculationService.Verify(s => s.CalculateRefundAsync(
                It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<Usage>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<ProviderCostRefundContext?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private async Task<string> AddDebitAsync(int groupId, decimal amount, decimal balanceAfter)
        {
            var debit = new VirtualKeyGroupTransaction
            {
                VirtualKeyGroupId = groupId,
                TransactionType = TransactionType.Debit,
                Amount = amount,
                BalanceAfter = balanceAfter,
                ReferenceType = ReferenceType.System
            };
            _dbContext.VirtualKeyGroupTransactions.Add(debit);
            await _dbContext.SaveChangesAsync();
            return debit.Id.ToString();
        }

        public void Dispose()
        {
            _concreteDbContext?.Dispose();
        }
    }
}
