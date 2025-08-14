using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace ConduitLLM.Tests.Configuration.Services
{

    /// <summary>
    /// Unit tests for the BatchSpendUpdateService to ensure correct balance tracking
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "BatchSpendUpdateService")]
    public class BatchSpendUpdateServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly TestRedisConnectionFactory _testRedisFactory;
        private readonly Mock<ILogger<BatchSpendUpdateService>> _mockLogger;
        private readonly Mock<IConnectionMultiplexer> _mockRedisConnection;
        private readonly Mock<IDatabase> _mockRedisDb;
        private readonly Mock<IServer> _mockRedisServer;
        private readonly BatchSpendUpdateService _service;
        private readonly IConfigurationDbContext _dbContext;
        private readonly ConduitDbContext _concreteDbContext;
        private readonly Mock<IVirtualKeyGroupRepository> _mockGroupRepository;

        public BatchSpendUpdateServiceTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _concreteDbContext = new ConduitDbContext(options);
            _dbContext = _concreteDbContext;

            // Setup Redis mocks
            _mockRedisDb = new Mock<IDatabase>();
            _mockRedisServer = new Mock<IServer>();
            _mockRedisConnection = new Mock<IConnectionMultiplexer>();
            _testRedisFactory = new TestRedisConnectionFactory(_mockRedisConnection.Object);
            
            var endPoint = new System.Net.DnsEndPoint("localhost", 6379);
            _mockRedisConnection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockRedisDb.Object);
            _mockRedisConnection.Setup(x => x.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
                .Returns(_mockRedisServer.Object);
            _mockRedisConnection.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
                .Returns(new[] { endPoint });
            
            // Redis factory is now handled by TestRedisConnectionFactory

            // Setup service provider
            _mockGroupRepository = new Mock<IVirtualKeyGroupRepository>();
            
            var services = new ServiceCollection();
            services.AddSingleton<IConfigurationDbContext>(_dbContext);
            services.AddSingleton(_mockGroupRepository.Object);
            services.AddLogging();
            
            _serviceProvider = services.BuildServiceProvider();
            
            _mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            mockScope.Setup(x => x.ServiceProvider).Returns(_serviceProvider);
            _mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            
            _mockLogger = new Mock<ILogger<BatchSpendUpdateService>>();
            
            var batchOptions = Microsoft.Extensions.Options.Options.Create(new BatchSpendingOptions()); // Use defaults
            _service = new BatchSpendUpdateService(
                _mockScopeFactory.Object,
                _testRedisFactory,
                batchOptions,
                _mockLogger.Object);
        }

        [Fact]
        public async Task FlushPendingUpdates_ShouldCreateSingleTransactionWithCorrectBalance()
        {
            // Arrange
            var groupId = 1;
            var initialBalance = 100m;
            var usageCost = 5.25m;
            var expectedBalance = initialBalance - usageCost;
            
            // Setup virtual key group in database
            var group = new VirtualKeyGroup
            {
                Id = groupId,
                GroupName = "Test Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.VirtualKeyGroups.Add(group);
            
            // Setup virtual key
            var virtualKey = new VirtualKey
            {
                Id = 1,
                VirtualKeyGroupId = groupId,
                KeyName = "Test Key",
                KeyHash = "testhash123",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.VirtualKeys.Add(virtualKey);
            await _dbContext.SaveChangesAsync();
            
            // Setup Redis to return pending spend data
            var redisKeys = new RedisKey[] { $"pending_spend:group:{groupId}" };
            _mockRedisServer.Setup(x => x.Keys(
                It.IsAny<int>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Returns(redisKeys);
            
            _mockRedisDb.Setup(x => x.StringGetDeleteAsync(
                It.Is<RedisKey>(k => k == $"pending_spend:group:{groupId}"), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(usageCost.ToString()));
            
            // Setup key usage data
            var keyUsageKeys = new RedisKey[] { $"key_usage:group:{groupId}:key:1" };
            _mockRedisServer.Setup(x => x.Keys(
                It.IsAny<int>(), 
                It.Is<RedisValue>(v => v == "key_usage:group:*"), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Returns(keyUsageKeys);
            
            _mockRedisDb.Setup(x => x.StringGetDeleteAsync(
                It.Is<RedisKey>(k => k == $"key_usage:group:{groupId}:key:1"), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(usageCost.ToString()));
            
            // Setup the repository mock to adjust balance correctly
            _mockGroupRepository.Setup(x => x.AdjustBalanceAsync(
                groupId, 
                -usageCost, 
                It.IsAny<string>(), 
                "System"))
                .ReturnsAsync(expectedBalance)
                .Callback<int, decimal, string, string>((gId, amount, desc, initiatedBy) =>
                {
                    // Simulate what the real repository does
                    group.Balance += amount;
                    group.LifetimeSpent += Math.Abs(amount);
                    group.UpdatedAt = DateTime.UtcNow;
                    
                    var transaction = new VirtualKeyGroupTransaction
                    {
                        VirtualKeyGroupId = gId,
                        TransactionType = TransactionType.Debit,
                        Amount = Math.Abs(amount),
                        BalanceAfter = group.Balance,
                        Description = desc,
                        InitiatedBy = initiatedBy,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.VirtualKeyGroupTransactions.Add(transaction);
                    _concreteDbContext.SaveChanges();
                });
            
            // Act
            var result = await _service.FlushPendingUpdatesAsync();
            
            // Assert
            Assert.Equal(1, result); // One group was updated
            
            // Verify only one transaction was created
            var transactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId)
                .ToListAsync();
            
            Assert.Single(transactions);
            
            var transaction = transactions.First();
            Assert.Equal(TransactionType.Debit, transaction.TransactionType);
            Assert.Equal(usageCost, transaction.Amount);
            Assert.Equal(expectedBalance, transaction.BalanceAfter); // This is the key assertion
            Assert.Contains("API usage", transaction.Description);
            Assert.Equal("System", transaction.InitiatedBy);
            
            // Verify the group balance was updated correctly
            var updatedGroup = await _dbContext.VirtualKeyGroups.FindAsync(groupId);
            Assert.Equal(expectedBalance, updatedGroup.Balance);
            Assert.Equal(usageCost, updatedGroup.LifetimeSpent);
        }

        [Fact]
        public async Task FlushPendingUpdates_WithMultipleKeys_ShouldCreateSingleTransactionWithAggregatedUsage()
        {
            // Arrange
            var groupId = 1;
            var initialBalance = 100m;
            var key1Usage = 3.50m;
            var key2Usage = 2.75m;
            var totalUsage = key1Usage + key2Usage;
            var expectedBalance = initialBalance - totalUsage;
            
            // Setup virtual key group
            var group = new VirtualKeyGroup
            {
                Id = groupId,
                GroupName = "Test Group",
                Balance = initialBalance,
                LifetimeCreditsAdded = initialBalance,
                LifetimeSpent = 0
            };
            _dbContext.VirtualKeyGroups.Add(group);
            
            // Setup virtual keys
            _dbContext.VirtualKeys.AddRange(
                new VirtualKey { Id = 1, VirtualKeyGroupId = groupId, KeyHash = "key1" },
                new VirtualKey { Id = 2, VirtualKeyGroupId = groupId, KeyHash = "key2" }
            );
            await _dbContext.SaveChangesAsync();
            
            // Setup Redis for group total
            _mockRedisServer.Setup(x => x.Keys(
                It.IsAny<int>(), 
                It.Is<RedisValue>(v => v == "pending_spend:group:*"), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Returns(new RedisKey[] { $"pending_spend:group:{groupId}" });
            
            _mockRedisDb.Setup(x => x.StringGetDeleteAsync(
                It.Is<RedisKey>(k => k == $"pending_spend:group:{groupId}"), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(totalUsage.ToString()));
            
            // Setup key usage data
            var keyUsageKeys = new RedisKey[] 
            { 
                $"key_usage:group:{groupId}:key:1",
                $"key_usage:group:{groupId}:key:2"
            };
            _mockRedisServer.Setup(x => x.Keys(
                It.IsAny<int>(), 
                It.Is<RedisValue>(v => v == "key_usage:group:*"), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Returns(keyUsageKeys);
            
            _mockRedisDb.Setup(x => x.StringGetDeleteAsync(
                It.Is<RedisKey>(k => k == $"key_usage:group:{groupId}:key:1"), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(key1Usage.ToString()));
            
            _mockRedisDb.Setup(x => x.StringGetDeleteAsync(
                It.Is<RedisKey>(k => k == $"key_usage:group:{groupId}:key:2"), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(key2Usage.ToString()));
            
            // Setup repository mock
            _mockGroupRepository.Setup(x => x.AdjustBalanceAsync(
                groupId, 
                -totalUsage, 
                It.Is<string>(s => s.Contains("2 virtual keys")), 
                "System"))
                .ReturnsAsync(expectedBalance)
                .Callback<int, decimal, string, string>((gId, amount, desc, initiatedBy) =>
                {
                    group.Balance += amount;
                    group.LifetimeSpent += Math.Abs(amount);
                    
                    var transaction = new VirtualKeyGroupTransaction
                    {
                        VirtualKeyGroupId = gId,
                        TransactionType = TransactionType.Debit,
                        Amount = Math.Abs(amount),
                        BalanceAfter = group.Balance,
                        Description = desc,
                        InitiatedBy = initiatedBy,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.VirtualKeyGroupTransactions.Add(transaction);
                    _concreteDbContext.SaveChanges();
                });
            
            // Act
            var result = await _service.FlushPendingUpdatesAsync();
            
            // Assert
            Assert.Equal(1, result);
            
            var transactions = await _dbContext.VirtualKeyGroupTransactions
                .Where(t => t.VirtualKeyGroupId == groupId)
                .ToListAsync();
            
            // Should only have ONE transaction for the aggregated usage
            Assert.Single(transactions);
            
            var transaction = transactions.First();
            Assert.Equal(totalUsage, transaction.Amount);
            Assert.Equal(expectedBalance, transaction.BalanceAfter);
            Assert.Contains("2 virtual keys", transaction.Description);
        }

        [Fact]
        public async Task QueueSpendUpdate_ShouldAccumulateSpendInRedis()
        {
            // Arrange
            var virtualKeyId = 1;
            var groupId = 1;
            var cost = 0.05m;
            
            // Setup virtual key in database
            var virtualKey = new VirtualKey
            {
                Id = virtualKeyId,
                VirtualKeyGroupId = groupId,
                KeyHash = "testkey"
            };
            _dbContext.VirtualKeys.Add(virtualKey);
            await _dbContext.SaveChangesAsync();
            
            // Setup Redis mocks
            _mockRedisDb.Setup(x => x.StringIncrementAsync(
                It.Is<RedisKey>(k => k == $"pending_spend:group:{groupId}"), 
                It.Is<double>(d => Math.Abs(d - (double)cost) < 0.0001),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync((double)cost);
            
            _mockRedisDb.Setup(x => x.StringIncrementAsync(
                It.Is<RedisKey>(k => k == $"key_usage:group:{groupId}:key:{virtualKeyId}"), 
                It.Is<double>(d => Math.Abs(d - (double)cost) < 0.0001),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync((double)cost);
            
            _mockRedisDb.Setup(x => x.KeyExpireAsync(
                It.IsAny<RedisKey>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
            
            // Act
            _service.QueueSpendUpdate(virtualKeyId, cost);
            
            // Give the async task time to complete
            await Task.Delay(100);
            
            // Assert
            _mockRedisDb.Verify(x => x.StringIncrementAsync(
                It.Is<RedisKey>(k => k == $"pending_spend:group:{groupId}"), 
                It.IsAny<double>(),
                It.IsAny<CommandFlags>()), 
                Times.Once);
            
            _mockRedisDb.Verify(x => x.StringIncrementAsync(
                It.Is<RedisKey>(k => k == $"key_usage:group:{groupId}:key:{virtualKeyId}"), 
                It.IsAny<double>(),
                It.IsAny<CommandFlags>()), 
                Times.Once);
        }

        [Fact]
        public async Task FlushPendingUpdates_WithNoData_ShouldReturnZero()
        {
            // Arrange
            _mockRedisServer.Setup(x => x.Keys(
                It.IsAny<int>(), 
                It.IsAny<RedisValue>(), 
                It.IsAny<int>(), 
                It.IsAny<long>(), 
                It.IsAny<int>(), 
                It.IsAny<CommandFlags>()))
                .Returns(Array.Empty<RedisKey>());
            
            // Act
            var result = await _service.FlushPendingUpdatesAsync();
            
            // Assert
            Assert.Equal(0, result);
            
            // Verify no transactions were created
            var transactions = await _dbContext.VirtualKeyGroupTransactions.ToListAsync();
            Assert.Empty(transactions);
        }

        public void Dispose()
        {
            _concreteDbContext?.Dispose();
            _serviceProvider?.Dispose();
            _service?.Dispose();
        }
    }
}