using ConduitLLM.WebUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Data;
using ConduitLLM.WebUI.Services;
using ConduitLLM.Tests.TestHelpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class GlobalSettingServiceTests
    {
        private readonly ILogger<GlobalSettingService> _logger;

        public GlobalSettingServiceTests()
        {
            _logger = NullLogger<GlobalSettingService>.Instance;
        }

        [Fact]
        public async Task GetSettingAsync_ShouldReturnValue_WhenSettingExists()
        {
            // Arrange
            string key = "TestKey";
            string expectedValue = "TestValue";
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = key, Value = expectedValue } 
                    });
            }
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            var result = await service.GetSettingAsync(key);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public async Task GetSettingAsync_ShouldReturnNull_WhenSettingDoesNotExist()
        {
            // Arrange
            string key = "NonExistentKey";
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // No need to seed the database as we're testing for non-existent setting
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            var result = await service.GetSettingAsync(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetSettingAsync_ShouldCreateNewSetting_WhenKeyDoesNotExist()
        {
            // Arrange
            string key = "NewKey";
            string value = "NewValue";
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            await service.SetSettingAsync(key, value);
            
            // Verify setting was created
            using (var context = await factory.CreateDbContextAsync())
            {
                var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
                Assert.NotNull(setting);
                Assert.Equal(value, setting.Value);
            }
        }
        
        [Fact]
        public async Task SetSettingAsync_ShouldUpdateExistingSetting_WhenKeyExists()
        {
            // Arrange
            string key = "ExistingKey";
            string originalValue = "OriginalValue";
            string newValue = "UpdatedValue";
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = key, Value = originalValue } 
                    });
            }
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            await service.SetSettingAsync(key, newValue);
            
            // Verify setting was updated
            using (var context = await factory.CreateDbContextAsync())
            {
                var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
                Assert.NotNull(setting);
                Assert.Equal(newValue, setting.Value);
            }
        }

        [Fact]
        public async Task GetMasterKeyHashAsync_ShouldReturnHash()
        {
            // Arrange
            string expectedHash = "abc123hash";
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "MasterKeyHash", Value = expectedHash } 
                    });
            }
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            var result = await service.GetMasterKeyHashAsync();

            // Assert
            Assert.Equal(expectedHash, result);
        }

        [Fact]
        public async Task GetMasterKeyHashAlgorithmAsync_ShouldReturnAlgorithm()
        {
            // Arrange
            string expectedAlgorithm = "SHA512";
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "MasterKeyHashAlgorithm", Value = expectedAlgorithm } 
                    });
            }
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            var result = await service.GetMasterKeyHashAlgorithmAsync();

            // Assert
            Assert.Equal(expectedAlgorithm, result);
        }

        [Fact]
        public async Task GetMasterKeyHashAlgorithmAsync_ShouldReturnDefaultSHA256_WhenNotSet()
        {
            // Arrange
            // Create in-memory database context factory (no need to seed data)
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            var result = await service.GetMasterKeyHashAlgorithmAsync();

            // Assert
            Assert.Equal("SHA256", result);
        }

        [Fact]
        public async Task SetMasterKeyAsync_ShouldHashAndSaveKey()
        {
            // Arrange
            string masterKey = "SecureMasterKey123";
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            await service.SetMasterKeyAsync(masterKey);

            // Assert - verify that both settings were saved
            using (var context = await factory.CreateDbContextAsync())
            {
                var hashSetting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "MasterKeyHash");
                Assert.NotNull(hashSetting);
                Assert.NotEmpty(hashSetting.Value);
                
                var algoSetting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "MasterKeyHashAlgorithm");
                Assert.NotNull(algoSetting);
                Assert.Equal("SHA256", algoSetting.Value);
            }
        }

        [Fact]
        public async Task SetMasterKeyAsync_ShouldThrowException_WhenKeyIsEmpty()
        {
            // Arrange
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.SetMasterKeyAsync(""));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SetMasterKeyAsync(null!));
        }
        
        [Fact]
        public async Task SetMasterKeyAsync_UsesCorrectHashAlgorithm()
        {
            // Arrange
            string masterKey = "SecureMasterKey123";
            
            // Calculate expected hash with SHA256
            string expectedHash;
            using (var sha256 = SHA256.Create())
            {
                var keyBytes = Encoding.UTF8.GetBytes(masterKey);
                var hashBytes = sha256.ComputeHash(keyBytes);
                expectedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Create service with real factory
            var service = new GlobalSettingService(factory, _logger);

            // Act
            await service.SetMasterKeyAsync(masterKey);

            // Assert - Verify the hash matches what we expect
            using (var context = await factory.CreateDbContextAsync())
            {
                var hashSetting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "MasterKeyHash");
                Assert.NotNull(hashSetting);
                Assert.Equal(expectedHash, hashSetting.Value);
            }
        }
    }
}