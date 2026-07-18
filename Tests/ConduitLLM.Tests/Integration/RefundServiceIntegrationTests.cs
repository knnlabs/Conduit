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
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRefundResult);

            // Act
            var result = await _refundService.ProcessRefundAsync(
                groupId,
                modelId,
                originalUsage,
                refundUsage,
                refundReason,
                null,
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
                    null,
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
                    null,
                    "TestAdmin",
                    null);
            });
        }

        public void Dispose()
        {
            _concreteDbContext?.Dispose();
        }
    }
}
