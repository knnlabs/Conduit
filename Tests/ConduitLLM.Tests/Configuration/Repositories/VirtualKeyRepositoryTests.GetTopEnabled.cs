using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    /// <summary>
    /// Unit tests for the VirtualKeyRepository.GetTopEnabledAsync method.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Repository")]
    public class VirtualKeyRepositoryGetTopEnabledTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly DbContextOptions<ConduitDbContext> _options;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockContextFactory;
        private readonly Mock<ILogger<VirtualKeyRepository>> _mockLogger;
        private readonly VirtualKeyRepository _repository;
        private readonly ITestOutputHelper _output;
        private readonly SqliteTestDatabase _database;

        public VirtualKeyRepositoryGetTopEnabledTests(ITestOutputHelper output)
        {
            _output = output;

            _database = new SqliteTestDatabase();
            _options = _database.Options;
            _context = _database.CreateContext();
            _mockContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            // The factory must return a new context each time to simulate production behavior
            _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _database.CreateContext());

            _mockLogger = new Mock<ILogger<VirtualKeyRepository>>();

            _repository = new VirtualKeyRepository(_mockContextFactory.Object, _mockLogger.Object);
        }

        #region GetTopEnabledAsync Tests

        [Fact]
        public async Task GetTopEnabledAsync_WithMultipleEnabledKeys_ReturnsRequestedCount()
        {
            // Arrange
            var keyGroup = await CreateTestKeyGroup("Test Group");

            // Create 5 enabled keys
            for (int i = 1; i <= 5; i++)
            {
                await CreateTestKey($"Key {i}", $"hash{i}", keyGroup.Id, isEnabled: true);
            }

            // Act
            var result = await _repository.GetTopEnabledAsync(3);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().OnlyContain(k => k.IsEnabled);
        }

        [Fact]
        public async Task GetTopEnabledAsync_WithFewerEnabledKeysThanRequested_ReturnsAllEnabled()
        {
            // Arrange
            var keyGroup = await CreateTestKeyGroup("Test Group");

            // Create only 2 enabled keys
            await CreateTestKey("Key 1", "hash1", keyGroup.Id, isEnabled: true);
            await CreateTestKey("Key 2", "hash2", keyGroup.Id, isEnabled: true);

            // Act
            var result = await _repository.GetTopEnabledAsync(10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().OnlyContain(k => k.IsEnabled);
        }

        [Fact]
        public async Task GetTopEnabledAsync_WithNoEnabledKeys_ReturnsEmptyList()
        {
            // Arrange
            var keyGroup = await CreateTestKeyGroup("Test Group");

            // Create only disabled keys
            await CreateTestKey("Key 1", "hash1", keyGroup.Id, isEnabled: false);
            await CreateTestKey("Key 2", "hash2", keyGroup.Id, isEnabled: false);

            // Act
            var result = await _repository.GetTopEnabledAsync(5);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTopEnabledAsync_WithMixedEnabledDisabled_ReturnsOnlyEnabled()
        {
            // Arrange
            var keyGroup = await CreateTestKeyGroup("Test Group");

            // Create mix of enabled and disabled keys
            await CreateTestKey("Enabled Key 1", "hash1", keyGroup.Id, isEnabled: true);
            await CreateTestKey("Disabled Key 1", "hash2", keyGroup.Id, isEnabled: false);
            await CreateTestKey("Enabled Key 2", "hash3", keyGroup.Id, isEnabled: true);
            await CreateTestKey("Disabled Key 2", "hash4", keyGroup.Id, isEnabled: false);
            await CreateTestKey("Enabled Key 3", "hash5", keyGroup.Id, isEnabled: true);

            // Act
            var result = await _repository.GetTopEnabledAsync(10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().OnlyContain(k => k.IsEnabled);
            result.Select(k => k.KeyName).Should().NotContain(name => name.Contains("Disabled"));
        }

        [Fact]
        public async Task GetTopEnabledAsync_ReturnsKeysOrderedByKeyName()
        {
            // Arrange
            var keyGroup = await CreateTestKeyGroup("Test Group");

            // Create keys in non-alphabetical order
            await CreateTestKey("Zebra Key", "hashZ", keyGroup.Id, isEnabled: true);
            await CreateTestKey("Alpha Key", "hashA", keyGroup.Id, isEnabled: true);
            await CreateTestKey("Mike Key", "hashM", keyGroup.Id, isEnabled: true);
            await CreateTestKey("Beta Key", "hashB", keyGroup.Id, isEnabled: true);

            // Act
            var result = await _repository.GetTopEnabledAsync(10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(4);
            result.Should().BeInAscendingOrder(k => k.KeyName);
            result[0].KeyName.Should().Be("Alpha Key");
            result[1].KeyName.Should().Be("Beta Key");
            result[2].KeyName.Should().Be("Mike Key");
            result[3].KeyName.Should().Be("Zebra Key");
        }

        [Fact]
        public async Task GetTopEnabledAsync_WithZeroCount_ReturnsEmptyList()
        {
            // Arrange
            var keyGroup = await CreateTestKeyGroup("Test Group");
            await CreateTestKey("Key 1", "hash1", keyGroup.Id, isEnabled: true);

            // Act
            var result = await _repository.GetTopEnabledAsync(0);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTopEnabledAsync_WithEmptyDatabase_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetTopEnabledAsync(5);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTopEnabledAsync_RespectsTakeCount_WhenMoreKeysExist()
        {
            // Arrange
            var keyGroup = await CreateTestKeyGroup("Test Group");

            // Create 10 enabled keys
            for (int i = 1; i <= 10; i++)
            {
                await CreateTestKey($"Key {i:D2}", $"hash{i}", keyGroup.Id, isEnabled: true);
            }

            // Act
            var result = await _repository.GetTopEnabledAsync(5);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(5);
            // Should return first 5 alphabetically
            result.Select(k => k.KeyName).Should().BeEquivalentTo(
                new[] { "Key 01", "Key 02", "Key 03", "Key 04", "Key 05" });
        }

        #endregion

        #region Helper Methods

        private async Task<VirtualKeyGroup> CreateTestKeyGroup(string groupName)
        {
            var keyGroup = new VirtualKeyGroup
            {
                GroupName = groupName,
                Balance = 100.0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.VirtualKeyGroups.Add(keyGroup);
            await _context.SaveChangesAsync();
            return keyGroup;
        }

        private async Task<VirtualKey> CreateTestKey(
            string keyName,
            string keyHash,
            int groupId,
            bool isEnabled)
        {
            var key = new VirtualKey
            {
                KeyName = keyName,
                KeyHash = keyHash,
                IsEnabled = isEnabled,
                VirtualKeyGroupId = groupId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.VirtualKeys.Add(key);
            await _context.SaveChangesAsync();
            return key;
        }

        #endregion

        public void Dispose()
        {
            _context?.Dispose();
            _database.Dispose();
        }
    }
}
