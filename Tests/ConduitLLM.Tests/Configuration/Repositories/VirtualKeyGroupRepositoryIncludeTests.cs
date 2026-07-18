using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    /// <summary>
    /// Integration tests to verify that Entity Framework Include() functionality works correctly
    /// with concrete DbContext instead of interface
    /// </summary>
    public class VirtualKeyGroupRepositoryIncludeTests : IDisposable
    {
        private readonly DbContextOptions<ConduitDbContext> _options;
        private readonly VirtualKeyGroupRepository _repository;
        private readonly Mock<ILogger<VirtualKeyGroupRepository>> _loggerMock;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _dbContextFactoryMock;

        public VirtualKeyGroupRepositoryIncludeTests()
        {
            // Use in-memory database for testing
            _options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContextFactoryMock = new Mock<IDbContextFactory<ConduitDbContext>>();
            _dbContextFactoryMock
                .Setup(f => f.CreateDbContext())
                .Returns(() => new ConduitDbContext(_options));
            _dbContextFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConduitDbContext(_options));

            _loggerMock = new Mock<ILogger<VirtualKeyGroupRepository>>();
            _repository = new VirtualKeyGroupRepository(_dbContextFactoryMock.Object, _loggerMock.Object);

            // Seed test data
            SeedTestData();
        }

        private void SeedTestData()
        {
            using var context = new ConduitDbContext(_options);

            // Create test groups
            var group1 = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group 1",
                Balance = 100,
                LifetimeCreditsAdded = 100,
                LifetimeSpent = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var group2 = new VirtualKeyGroup
            {
                Id = 2,
                GroupName = "Test Group 2",
                Balance = 200,
                LifetimeCreditsAdded = 200,
                LifetimeSpent = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.VirtualKeyGroups.Add(group1);
            context.VirtualKeyGroups.Add(group2);

            // Create test virtual keys
            var key1 = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key 1",
                KeyHash = "hash1",
                VirtualKeyGroupId = 1,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var key2 = new VirtualKey
            {
                Id = 2,
                KeyName = "Test Key 2",
                KeyHash = "hash2",
                VirtualKeyGroupId = 1,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var key3 = new VirtualKey
            {
                Id = 3,
                KeyName = "Test Key 3",
                KeyHash = "hash3",
                VirtualKeyGroupId = 2,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.VirtualKeys.AddRange(key1, key2, key3);
            context.SaveChanges();
        }

        [Fact]
        public async Task GetAllAsync_Should_Include_VirtualKeys()
        {
            // Act
#pragma warning disable CS0618 // Type or member is obsolete
            var groups = await _repository.GetAllAsync();
#pragma warning restore CS0618 // Type or member is obsolete

            // Assert
            Assert.NotNull(groups);
            Assert.Equal(2, groups.Count);

            var group1 = groups.FirstOrDefault(g => g.Id == 1);
            Assert.NotNull(group1);
            Assert.NotNull(group1.VirtualKeys);
            Assert.Equal(2, group1.VirtualKeys.Count);
            Assert.Contains(group1.VirtualKeys, k => k.KeyName == "Test Key 1");
            Assert.Contains(group1.VirtualKeys, k => k.KeyName == "Test Key 2");

            var group2 = groups.FirstOrDefault(g => g.Id == 2);
            Assert.NotNull(group2);
            Assert.NotNull(group2.VirtualKeys);
            Assert.Single(group2.VirtualKeys);
            Assert.Contains(group2.VirtualKeys, k => k.KeyName == "Test Key 3");
        }

        [Fact]
        public async Task GetByIdWithKeysAsync_Should_Include_VirtualKeys()
        {
            // Act
            var group = await _repository.GetByIdWithKeysAsync(1);

            // Assert
            Assert.NotNull(group);
            Assert.Equal("Test Group 1", group.GroupName);
            Assert.NotNull(group.VirtualKeys);
            Assert.Equal(2, group.VirtualKeys.Count);
            Assert.All(group.VirtualKeys, k => Assert.Equal(1, k.VirtualKeyGroupId));
        }

        [Fact]
        public async Task GetByIdAsync_Should_Include_VirtualKeys_By_Default()
        {
            // Act
            var group = await _repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(group);
            Assert.Equal("Test Group 1", group.GroupName);
            // With the new RepositoryBase pattern, ApplyDefaultIncludes includes VirtualKeys
            Assert.NotNull(group.VirtualKeys);
            Assert.Equal(2, group.VirtualKeys.Count);
        }

        [Fact]
        public async Task Repository_Should_Work_With_DbContextFactory()
        {
            // This test verifies that the repository works correctly with IDbContextFactory
            // using the new RepositoryBase pattern

            // Act & Assert - various operations should work
#pragma warning disable CS0618 // Type or member is obsolete
            var allGroups = await _repository.GetAllAsync();
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.NotEmpty(allGroups);

            var specificGroup = await _repository.GetByIdAsync(1);
            Assert.NotNull(specificGroup);

            var groupWithKeys = await _repository.GetByIdWithKeysAsync(1);
            Assert.NotNull(groupWithKeys);
            Assert.NotEmpty(groupWithKeys.VirtualKeys);
        }

        [Fact]
        public async Task GetPaginatedAsync_Should_Return_Correct_Page()
        {
            // Act
            var (items, totalCount) = await _repository.GetPaginatedAsync(1, 10);

            // Assert
            Assert.Equal(2, totalCount);
            Assert.Equal(2, items.Count);
            // Should be ordered by GroupName
            Assert.Equal("Test Group 1", items[0].GroupName);
            Assert.Equal("Test Group 2", items[1].GroupName);
        }

        [Fact]
        public async Task ExistsAsync_Should_Return_True_For_Existing_Group()
        {
            // Act
            var exists = await _repository.ExistsAsync(1);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_Should_Return_False_For_NonExisting_Group()
        {
            // Act
            var exists = await _repository.ExistsAsync(999);

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task CountAsync_Should_Return_Correct_Count()
        {
            // Act
            var count = await _repository.CountAsync();

            // Assert
            Assert.Equal(2, count);
        }

        public void Dispose()
        {
            // Clean up any resources if needed
        }
    }
}
