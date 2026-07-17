using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    /// <summary>
    /// Tests for the idempotent balance adjustment used by spend processing (#927):
    /// one application per idempotency key, with the key recorded on the ledger row
    /// in the same atomic save as the balance change.
    /// </summary>
    public class VirtualKeyGroupRepositoryIdempotencyTests
    {
        private readonly DbContextOptions<ConduitDbContext> _options;
        private readonly VirtualKeyGroupRepository _repository;

        public VirtualKeyGroupRepositoryIdempotencyTests()
        {
            _options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var dbContextFactoryMock = new Mock<IDbContextFactory<ConduitDbContext>>();
            dbContextFactoryMock
                .Setup(f => f.CreateDbContext())
                .Returns(() => new ConduitDbContext(_options));
            dbContextFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConduitDbContext(_options));

            _repository = new VirtualKeyGroupRepository(
                dbContextFactoryMock.Object,
                new Mock<ILogger<VirtualKeyGroupRepository>>().Object);

            using var context = new ConduitDbContext(_options);
            context.VirtualKeyGroups.Add(new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 100m,
                LifetimeCreditsAdded = 100m,
                LifetimeSpent = 0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        [Fact]
        public async Task AdjustBalanceIdempotentAsync_SameKeyTwice_AppliesOnce()
        {
            // Act
            var first = await _repository.AdjustBalanceIdempotentAsync(
                1, -10m, "spend:req-1", "usage", "System", ReferenceType.VirtualKey, "1");
            var second = await _repository.AdjustBalanceIdempotentAsync(
                1, -10m, "spend:req-1", "usage", "System", ReferenceType.VirtualKey, "1");

            // Assert
            Assert.True(first.Applied);
            Assert.Equal(90m, first.NewBalance);
            Assert.Equal(10m, first.LifetimeSpent);

            Assert.False(second.Applied);
            Assert.Equal(90m, second.NewBalance);
            Assert.Equal(10m, second.LifetimeSpent);

            using var context = new ConduitDbContext(_options);
            var group = await context.VirtualKeyGroups.SingleAsync(g => g.Id == 1);
            Assert.Equal(90m, group.Balance);
            Assert.Equal(10m, group.LifetimeSpent);

            var ledgerRows = await context.VirtualKeyGroupTransactions
                .Where(t => t.IdempotencyKey == "spend:req-1")
                .ToListAsync();
            Assert.Single(ledgerRows);
        }

        [Fact]
        public async Task AdjustBalanceIdempotentAsync_DistinctKeys_BothApply()
        {
            // Act
            var first = await _repository.AdjustBalanceIdempotentAsync(
                1, -10m, "spend:req-a", "usage", "System", ReferenceType.VirtualKey, "1");
            var second = await _repository.AdjustBalanceIdempotentAsync(
                1, -20m, "spend:req-b", "usage", "System", ReferenceType.VirtualKey, "1");

            // Assert
            Assert.True(first.Applied);
            Assert.True(second.Applied);
            Assert.Equal(70m, second.NewBalance);
            Assert.Equal(30m, second.LifetimeSpent);
        }

        [Fact]
        public async Task AdjustBalanceIdempotentAsync_WithMissingGroup_Throws()
        {
            // Act
            var act = () => _repository.AdjustBalanceIdempotentAsync(
                999, -10m, "spend:req-x", "usage", "System", ReferenceType.VirtualKey, "1");

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(act);
        }

        [Fact]
        public async Task AdjustBalanceIdempotentAsync_WithEmptyKey_Throws()
        {
            // Act
            var act = () => _repository.AdjustBalanceIdempotentAsync(
                1, -10m, " ", "usage", "System", ReferenceType.VirtualKey, "1");

            // Assert
            await Assert.ThrowsAsync<ArgumentException>(act);
        }

        [Fact]
        public async Task AdjustBalanceAsync_StoresNoIdempotencyKey()
        {
            // Act
            await _repository.AdjustBalanceAsync(1, -10m, "usage", "System", ReferenceType.VirtualKey, "1");

            // Assert
            using var context = new ConduitDbContext(_options);
            var ledgerRow = await context.VirtualKeyGroupTransactions.SingleAsync();
            Assert.Null(ledgerRow.IdempotencyKey);
        }
    }
}
