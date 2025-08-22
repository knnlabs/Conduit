using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for virtual key balance tracking to ensure
    /// transactions are recorded correctly with proper balance calculations
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Component", "VirtualKeyBalanceTracking")]
    public class VirtualKeyBalanceTrackingTests : IDisposable
    {
        private readonly IConfigurationDbContext _dbContext;
        private readonly ConduitDbContext _concreteDbContext;
        private readonly VirtualKeyGroupRepository _repository;
        private readonly Mock<ILogger<VirtualKeyGroupRepository>> _mockLogger;

        public VirtualKeyBalanceTrackingTests()
        {
            // Setup in-memory database for integration testing
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _concreteDbContext = new ConduitDbContext(options);
            _dbContext = _concreteDbContext;
            
            _mockLogger = new Mock<ILogger<VirtualKeyGroupRepository>>();
            _repository = new VirtualKeyGroupRepository(_concreteDbContext, _mockLogger.Object);
        }

        [Fact]
        public async Task AdjustBalance_ForDebit_ShouldCreateCorrectTransaction()
        {
            // Arrange
            var initialBalance = 100m;
            var usageAmount = 15.75m;
            var expectedBalance = initialBalance - usageAmount;
            
            var group = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 0
            };
            
            var groupId = await _repository.CreateAsync(group);
            
            // Act
            var newBalance = await _repository.AdjustBalanceAsync(
                groupId, 
                -usageAmount, 
                "API usage test",
                "TestUser");
            
            // Assert
            Assert.Equal(expectedBalance, newBalance);
            
            // Verify the group was updated correctly
            var updatedGroup = await _repository.GetByIdAsync(groupId);
            Assert.NotNull(updatedGroup);
            Assert.Equal(expectedBalance, updatedGroup.Balance);
            Assert.Equal(usageAmount, updatedGroup.LifetimeSpent);
            Assert.Equal(initialBalance, updatedGroup.LifetimeCreditsAdded);
            
            // Verify transaction was created with correct values
            var transactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId && t.TransactionType == TransactionType.Debit)
                .ToListAsync();
            
            Assert.Single(transactions);
            var transaction = transactions.First();
            
            Assert.Equal(TransactionType.Debit, transaction.TransactionType);
            Assert.Equal(usageAmount, transaction.Amount); // Should be positive
            Assert.Equal(expectedBalance, transaction.BalanceAfter); // Should match new balance
            Assert.Equal("API usage test", transaction.Description);
            Assert.Equal("TestUser", transaction.InitiatedBy);
            Assert.Equal(ReferenceType.Manual, transaction.ReferenceType);
        }

        [Fact]
        public async Task AdjustBalance_ForCredit_ShouldCreateCorrectTransaction()
        {
            // Arrange
            var initialBalance = 50m;
            var creditAmount = 25m;
            var expectedBalance = initialBalance + creditAmount;
            
            var group = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 10m
            };
            
            var groupId = await _repository.CreateAsync(group);
            
            // Act
            var newBalance = await _repository.AdjustBalanceAsync(
                groupId, 
                creditAmount, 
                "Credits added",
                "AdminUser");
            
            // Assert
            Assert.Equal(expectedBalance, newBalance);
            
            // Verify the group was updated correctly
            var updatedGroup = await _repository.GetByIdAsync(groupId);
            Assert.NotNull(updatedGroup);
            Assert.Equal(expectedBalance, updatedGroup.Balance);
            Assert.Equal(10m, updatedGroup.LifetimeSpent); // Should not change
            Assert.Equal(initialBalance + creditAmount, updatedGroup.LifetimeCreditsAdded);
            
            // Verify transaction was created with correct values (excluding initial balance transaction)
            var transactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId 
                    && t.TransactionType == TransactionType.Credit
                    && t.ReferenceType != ReferenceType.Initial)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            
            Assert.Single(transactions);
            var transaction = transactions.First();
            
            Assert.Equal(TransactionType.Credit, transaction.TransactionType);
            Assert.Equal(creditAmount, transaction.Amount); // Should be positive
            Assert.Equal(expectedBalance, transaction.BalanceAfter); // Should match new balance
            Assert.Equal("Credits added", transaction.Description);
            Assert.Equal("AdminUser", transaction.InitiatedBy);
        }

        [Fact]
        public async Task MultipleAdjustments_ShouldMaintainCorrectBalanceHistory()
        {
            // Arrange
            var initialBalance = 100m;
            var group = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 0
            };
            
            var groupId = await _repository.CreateAsync(group);
            
            // Act - Perform multiple transactions
            // Transaction 1: Debit $10
            await _repository.AdjustBalanceAsync(groupId, -10m, "Usage 1", "System");
            
            // Transaction 2: Debit $5.50
            await _repository.AdjustBalanceAsync(groupId, -5.50m, "Usage 2", "System");
            
            // Transaction 3: Credit $20
            await _repository.AdjustBalanceAsync(groupId, 20m, "Refill", "Admin");
            
            // Transaction 4: Debit $2.25
            await _repository.AdjustBalanceAsync(groupId, -2.25m, "Usage 3", "System");
            
            // Assert
            var finalGroup = await _repository.GetByIdAsync(groupId);
            var expectedFinalBalance = initialBalance - 10m - 5.50m + 20m - 2.25m;
            Assert.Equal(expectedFinalBalance, finalGroup.Balance);
            Assert.Equal(17.75m, finalGroup.LifetimeSpent); // 10 + 5.50 + 2.25
            Assert.Equal(120m, finalGroup.LifetimeCreditsAdded); // 100 + 20
            
            // Verify all transactions have correct BalanceAfter values
            var allTransactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
            
            // Should have 5 transactions (1 initial + 4 adjustments)
            Assert.Equal(5, allTransactions.Count);
            
            // Check each transaction has the correct balance after
            Assert.Equal(100m, allTransactions[0].BalanceAfter); // Initial
            Assert.Equal(90m, allTransactions[1].BalanceAfter);  // After -10
            Assert.Equal(84.50m, allTransactions[2].BalanceAfter); // After -5.50
            Assert.Equal(104.50m, allTransactions[3].BalanceAfter); // After +20
            Assert.Equal(102.25m, allTransactions[4].BalanceAfter); // After -2.25
        }

        [Fact]
        public async Task AdjustBalance_WithNullDescription_ShouldUseDefaultDescription()
        {
            // Arrange
            var group = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = 100m,
                LifetimeCreditsAdded = 100m,
                LifetimeSpent = 0
            };
            
            var groupId = await _repository.CreateAsync(group);
            
            // Act - Debit without description
            await _repository.AdjustBalanceAsync(groupId, -10m, null, "System");
            
            // Act - Credit without description  
            await _repository.AdjustBalanceAsync(groupId, 5m, null, "System");
            
            // Assert
            var transactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
            
            // Find the debit and credit transactions (skip initial)
            var debitTransaction = transactions.FirstOrDefault(t => t.TransactionType == TransactionType.Debit);
            var creditTransaction = transactions.FirstOrDefault(t => t.TransactionType == TransactionType.Credit && t.Amount == 5m);
            
            Assert.NotNull(debitTransaction);
            Assert.NotNull(creditTransaction);
            Assert.Equal("Usage deducted", debitTransaction.Description);
            Assert.Equal("Credits added", creditTransaction.Description);
        }

        [Fact]
        public async Task CreateGroup_WithInitialBalance_ShouldCreateInitialTransaction()
        {
            // Arrange & Act
            var initialBalance = 50m;
            var group = new VirtualKeyGroup
            {
                GroupName = "New Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 0
            };
            
            var groupId = await _repository.CreateAsync(group);
            
            // Assert
            var transactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId)
                .ToListAsync();
            
            Assert.Single(transactions);
            var initialTransaction = transactions.First();
            
            Assert.Equal(TransactionType.Credit, initialTransaction.TransactionType);
            Assert.Equal(initialBalance, initialTransaction.Amount);
            Assert.Equal(initialBalance, initialTransaction.BalanceAfter);
            Assert.Equal("Initial balance", initialTransaction.Description);
            Assert.Equal(ReferenceType.Initial, initialTransaction.ReferenceType);
        }

        [Fact]
        public async Task AdjustBalance_ForNonExistentGroup_ShouldThrowException()
        {
            // Arrange
            var nonExistentGroupId = 9999;
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _repository.AdjustBalanceAsync(nonExistentGroupId, -10m, "Test", "User"));
        }

        public void Dispose()
        {
            _concreteDbContext?.Dispose();
        }
    }
}