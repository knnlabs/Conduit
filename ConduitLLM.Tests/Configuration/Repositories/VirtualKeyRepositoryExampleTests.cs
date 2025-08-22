using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    /// <summary>
    /// Example unit tests for the VirtualKeyRepository class demonstrating repository testing patterns.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Repository")]
    public class VirtualKeyRepositoryExampleTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly DbContextOptions<ConduitDbContext> _options;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockContextFactory;
        private readonly Mock<ILogger<VirtualKeyRepository>> _mockLogger;
        private readonly VirtualKeyRepository _repository;
        private readonly ITestOutputHelper _output;

        public VirtualKeyRepositoryExampleTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Setup in-memory database for testing
            _options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _context = new ConduitDbContext(_options);
            _mockContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            // The factory must return a new context each time to simulate production behavior
            // where each operation gets its own context that will be disposed
            _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConduitDbContext(_options));
            
            _mockLogger = new Mock<ILogger<VirtualKeyRepository>>();
            
            _repository = new VirtualKeyRepository(_mockContextFactory.Object, _mockLogger.Object);
        }

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_WithExistingKey_ShouldReturnKey()
        {
            // Arrange
            // Create a VirtualKeyGroup first since GetByIdAsync now includes it
            var keyGroup = new VirtualKeyGroup
            {
                GroupName = "Test Group",
                Balance = 100.0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.VirtualKeyGroups.Add(keyGroup);
            await _context.SaveChangesAsync();
            
            var key = new VirtualKey
            {
                KeyName = "Test Key",
                KeyHash = "test-hash-123",
                IsEnabled = true,
                VirtualKeyGroupId = keyGroup.Id,
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
            result.VirtualKeyGroup.Should().NotBeNull();
            result.VirtualKeyGroup!.Id.Should().Be(keyGroup.Id);
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
                VirtualKeyGroupId = 1,
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
            dbKey.VirtualKeyGroupId.Should().Be(1);
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
            key.RateLimitRpm = 100;

            // Act
            var result = await _repository.UpdateAsync(key);

            // Assert
            result.Should().BeTrue();
            
            // Verify in database
            var dbKey = await _context.VirtualKeys.FindAsync(key.Id);
            dbKey!.KeyName.Should().Be("Updated Name");
            dbKey.IsEnabled.Should().BeFalse();
            dbKey.RateLimitRpm.Should().Be(100);
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
            using (var verifyContext = new ConduitDbContext(_options))
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

        // Note: BulkUpdateSpendAsync has been removed as spend tracking is now at the group level

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}