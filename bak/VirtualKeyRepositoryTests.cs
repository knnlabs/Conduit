using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Repositories
{
    public class VirtualKeyRepositoryTests
    {
        private readonly DbContextOptions<ConfigurationDbContext> _dbContextOptions;
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly Mock<ILogger<VirtualKeyRepository>> _loggerMock;
        
        public VirtualKeyRepositoryTests()
        {
            // Setup in-memory database for testing
            _dbContextOptions = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDbVirtualKeys_{Guid.NewGuid()}")
                .Options;
            
            // Setup DB context factory
            var dbContextFactoryMock = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            dbContextFactoryMock
                .Setup(f => f.CreateDbContext())
                .Returns(() => new ConfigurationDbContext(_dbContextOptions));
            
            dbContextFactoryMock
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConfigurationDbContext(_dbContextOptions));
            
            _dbContextFactory = dbContextFactoryMock.Object;
            
            // Setup logger mock
            _loggerMock = new Mock<ILogger<VirtualKeyRepository>>();
        }
        
        [Fact]
        public async Task CreateAsync_ShouldCreateVirtualKey()
        {
            // Arrange
            var repository = new VirtualKeyRepository(_dbContextFactory, _loggerMock.Object);
            var virtualKey = new VirtualKey
            {
                KeyName = "Test Key",
                KeyHash = "testhash123",
                AllowedModels = "model1,model2",
                MaxBudget = 100.0m,
                BudgetDuration = "Monthly",
                BudgetStartDate = DateTime.UtcNow.Date,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Act
            int id = await repository.CreateAsync(virtualKey);
            
            // Assert
            Assert.True(id > 0); // ID should be assigned
            
            // Verify the key exists in the database
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var savedKey = await context.VirtualKeys.FindAsync(id);
            
            Assert.NotNull(savedKey);
            Assert.Equal("Test Key", savedKey.KeyName);
            Assert.Equal("testhash123", savedKey.KeyHash);
        }
        
        [Fact]
        public async Task GetByIdAsync_ShouldReturnVirtualKey()
        {
            // Arrange
            var repository = new VirtualKeyRepository(_dbContextFactory, _loggerMock.Object);
            var virtualKey = new VirtualKey
            {
                KeyName = "Get By ID Test",
                KeyHash = "getbyidhash123",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Add the virtual key to the database
            using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                context.VirtualKeys.Add(virtualKey);
                await context.SaveChangesAsync();
            }
            
            // Act
            var result = await repository.GetByIdAsync(virtualKey.Id);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKey.Id, result.Id);
            Assert.Equal("Get By ID Test", result.KeyName);
        }
        
        [Fact]
        public async Task GetByKeyHashAsync_ShouldReturnVirtualKey()
        {
            // Arrange
            var repository = new VirtualKeyRepository(_dbContextFactory, _loggerMock.Object);
            var virtualKey = new VirtualKey
            {
                KeyName = "Hash Test",
                KeyHash = "uniquehash456",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Add the virtual key to the database
            using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                context.VirtualKeys.Add(virtualKey);
                await context.SaveChangesAsync();
            }
            
            // Act
            var result = await repository.GetByKeyHashAsync("uniquehash456");
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKey.Id, result.Id);
            Assert.Equal("Hash Test", result.KeyName);
        }
        
        [Fact]
        public async Task GetAllAsync_ShouldReturnAllVirtualKeys()
        {
            // Arrange
            var repository = new VirtualKeyRepository(_dbContextFactory, _loggerMock.Object);
            
            // Add multiple virtual keys to the database
            using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                context.VirtualKeys.AddRange(
                    new VirtualKey { KeyName = "Key 1", KeyHash = "hash1", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new VirtualKey { KeyName = "Key 2", KeyHash = "hash2", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new VirtualKey { KeyName = "Key 3", KeyHash = "hash3", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                );
                await context.SaveChangesAsync();
            }
            
            // Act
            var results = await repository.GetAllAsync();
            
            // Assert
            Assert.Equal(3, results.Count);
            Assert.Contains(results, k => k.KeyName == "Key 1");
            Assert.Contains(results, k => k.KeyName == "Key 2");
            Assert.Contains(results, k => k.KeyName == "Key 3");
        }
        
        [Fact]
        public async Task UpdateAsync_ShouldUpdateVirtualKey()
        {
            // Arrange
            var repository = new VirtualKeyRepository(_dbContextFactory, _loggerMock.Object);
            var virtualKey = new VirtualKey
            {
                KeyName = "Update Test",
                KeyHash = "updatehash789",
                IsEnabled = true,
                MaxBudget = 50m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Add the virtual key to the database
            using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                context.VirtualKeys.Add(virtualKey);
                await context.SaveChangesAsync();
            }
            
            // Update the virtual key
            virtualKey.KeyName = "Updated Name";
            virtualKey.MaxBudget = 100m;
            
            // Act
            var result = await repository.UpdateAsync(virtualKey);
            
            // Assert
            Assert.True(result);
            
            // Verify the key was updated in the database
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var updatedKey = await context.VirtualKeys.FindAsync(virtualKey.Id);
            
            Assert.NotNull(updatedKey);
            Assert.Equal("Updated Name", updatedKey.KeyName);
            Assert.Equal(100m, updatedKey.MaxBudget);
        }
        
        [Fact]
        public async Task DeleteAsync_ShouldDeleteVirtualKey()
        {
            // Arrange
            var repository = new VirtualKeyRepository(_dbContextFactory, _loggerMock.Object);
            var virtualKey = new VirtualKey
            {
                KeyName = "Delete Test",
                KeyHash = "deletehash001",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Add the virtual key to the database
            using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                context.VirtualKeys.Add(virtualKey);
                await context.SaveChangesAsync();
            }
            
            // Act
            var result = await repository.DeleteAsync(virtualKey.Id);
            
            // Assert
            Assert.True(result);
            
            // Verify the key was deleted from the database
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var deletedKey = await context.VirtualKeys.FindAsync(virtualKey.Id);
            
            Assert.Null(deletedKey);
        }
    }
}