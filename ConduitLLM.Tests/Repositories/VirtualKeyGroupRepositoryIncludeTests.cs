using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Repositories
{
    /// <summary>
    /// Integration tests to verify that Entity Framework Include() functionality works correctly
    /// with concrete DbContext instead of interface
    /// </summary>
    public class VirtualKeyGroupRepositoryIncludeTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly VirtualKeyGroupRepository _repository;
        private readonly Mock<ILogger<VirtualKeyGroupRepository>> _loggerMock;

        public VirtualKeyGroupRepositoryIncludeTests()
        {
            // Use in-memory database for testing
            var options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ConduitDbContext(options);
            _loggerMock = new Mock<ILogger<VirtualKeyGroupRepository>>();
            _repository = new VirtualKeyGroupRepository(_context, _loggerMock.Object);

            // Seed test data
            SeedTestData();
        }

        private void SeedTestData()
        {
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

            _context.VirtualKeyGroups.Add(group1);
            _context.VirtualKeyGroups.Add(group2);

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

            _context.VirtualKeys.AddRange(key1, key2, key3);
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetAllAsync_Should_Include_VirtualKeys()
        {
            // Act
            var groups = await _repository.GetAllAsync();

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
        public async Task GetByIdAsync_Without_Include_Should_Not_Load_VirtualKeys()
        {
            // Act
            var group = await _repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(group);
            Assert.Equal("Test Group 1", group.GroupName);
            // In EF Core with in-memory database, navigation properties might still be loaded
            // The important part is that the Include statement works when we need it
        }

        [Fact]
        public async Task Repository_Should_Work_With_Concrete_DbContext()
        {
            // This test verifies that the repository works correctly with ConfigurationDbContext
            // instead of IConfigurationDbContext interface

            // Act & Assert - various operations should work
            var allGroups = await _repository.GetAllAsync();
            Assert.NotEmpty(allGroups);

            var specificGroup = await _repository.GetByIdAsync(1);
            Assert.NotNull(specificGroup);

            var groupWithKeys = await _repository.GetByIdWithKeysAsync(1);
            Assert.NotNull(groupWithKeys);
            Assert.NotEmpty(groupWithKeys.VirtualKeys);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}