using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Repositories
{
    /// <summary>
    /// Example unit tests for the VirtualKeyRepository class demonstrating repository testing patterns.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Repository")]
    public class VirtualKeyRepositoryExampleTests : IDisposable
    {
        private readonly ConfigurationDbContext _context;
        private readonly DbContextOptions<ConfigurationDbContext> _options;
        private readonly Mock<IDbContextFactory<ConfigurationDbContext>> _mockContextFactory;
        private readonly Mock<ILogger<VirtualKeyRepository>> _mockLogger;
        private readonly VirtualKeyRepository _repository;
        private readonly ITestOutputHelper _output;

        public VirtualKeyRepositoryExampleTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Setup in-memory database for testing
            _options = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _context = new ConfigurationDbContext(_options);
            _mockContextFactory = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            // The factory must return a new context each time to simulate production behavior
            // where each operation gets its own context that will be disposed
            _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConfigurationDbContext(_options));
            
            _mockLogger = new Mock<ILogger<VirtualKeyRepository>>();
            
            _repository = new VirtualKeyRepository(_mockContextFactory.Object, _mockLogger.Object);
        }

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_WithExistingKey_ShouldReturnKey()
        {
            // Arrange
            var key = new VirtualKey
            {
                KeyName = "Test Key",
                KeyHash = "test-hash-123",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.VirtualKeys.Add(key);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(key.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(key.Id);
            result.KeyName.Should().Be("Test Key");
            result.KeyHash.Should().Be("test-hash-123");
        }

        [Fact]
        public async Task GetByIdAsync_WithNonExistingKey_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_WithValidKey_ShouldCreateAndReturnId()
        {
            // Arrange
            var key = new VirtualKey
            {
                KeyName = "New Key",
                KeyHash = "new-hash-456",
                IsEnabled = true,
                MaxBudget = 100m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var resultId = await _repository.CreateAsync(key);

            // Assert
            resultId.Should().BeGreaterThan(0);
            
            // Verify in database
            var dbKey = await _context.VirtualKeys.FindAsync(resultId);
            dbKey.Should().NotBeNull();
            dbKey!.KeyName.Should().Be("New Key");
            dbKey.KeyHash.Should().Be("new-hash-456");
            dbKey.MaxBudget.Should().Be(100m);
        }

        [Fact]
        public async Task CreateAsync_WithNullKey_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _repository.CreateAsync(null!));
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_WithExistingKey_ShouldReturnTrue()
        {
            // Arrange
            var key = new VirtualKey
            {
                KeyName = "Original Name",
                KeyHash = "update-hash-789",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.VirtualKeys.Add(key);
            await _context.SaveChangesAsync();
            
            // Detach to simulate separate context
            _context.Entry(key).State = EntityState.Detached;
            
            // Modify the key
            key.KeyName = "Updated Name";
            key.IsEnabled = false;
            key.MaxBudget = 500m;

            // Act
            var result = await _repository.UpdateAsync(key);

            // Assert
            result.Should().BeTrue();
            
            // Verify in database
            var dbKey = await _context.VirtualKeys.FindAsync(key.Id);
            dbKey!.KeyName.Should().Be("Updated Name");
            dbKey.IsEnabled.Should().BeFalse();
            dbKey.MaxBudget.Should().Be(500m);
        }

        [Fact]
        public async Task UpdateAsync_WithNonExistingKey_ShouldReturnFalse()
        {
            // Arrange
            var key = new VirtualKey
            {
                Id = 999,
                KeyName = "Non-existing",
                KeyHash = "non-existing-hash",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.UpdateAsync(key);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_WithExistingKey_ShouldReturnTrue()
        {
            // Arrange
            var key = new VirtualKey
            {
                KeyName = "To Delete",
                KeyHash = "delete-hash",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.VirtualKeys.Add(key);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.DeleteAsync(key.Id);

            // Assert
            result.Should().BeTrue();
            
            // Verify deletion using a new context
            using (var verifyContext = new ConfigurationDbContext(_options))
            {
                var deletedKey = await verifyContext.VirtualKeys.FindAsync(key.Id);
                deletedKey.Should().BeNull();
            }
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistingKey_ShouldReturnFalse()
        {
            // Act
            var result = await _repository.DeleteAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region BulkUpdateSpendAsync Tests

        [Fact]
        public async Task BulkUpdateSpendAsync_WithValidUpdates_ShouldUpdateAndReturnTrue()
        {
            // Arrange
            var keys = new List<VirtualKey>
            {
                new VirtualKey { KeyName = "Key1", KeyHash = "hash1", IsEnabled = true, CurrentSpend = 0 },
                new VirtualKey { KeyName = "Key2", KeyHash = "hash2", IsEnabled = true, CurrentSpend = 0 },
                new VirtualKey { KeyName = "Key3", KeyHash = "hash3", IsEnabled = true, CurrentSpend = 0 }
            };
            _context.VirtualKeys.AddRange(keys);
            await _context.SaveChangesAsync();

            var updates = new Dictionary<string, decimal>
            {
                { "hash1", 10.5m },
                { "hash2", 20.0m },
                { "hash3", 30.25m }
            };

            // Act
            var result = await _repository.BulkUpdateSpendAsync(updates);

            // Assert
            result.Should().BeTrue();
            
            // Verify updates using a new context
            using (var verifyContext = new ConfigurationDbContext(_options))
            {
                var updatedKeys = await verifyContext.VirtualKeys.ToListAsync();
                updatedKeys.First(k => k.KeyHash == "hash1").CurrentSpend.Should().Be(10.5m);
                updatedKeys.First(k => k.KeyHash == "hash2").CurrentSpend.Should().Be(20.0m);
                updatedKeys.First(k => k.KeyHash == "hash3").CurrentSpend.Should().Be(30.25m);
            }
        }

        #endregion

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}