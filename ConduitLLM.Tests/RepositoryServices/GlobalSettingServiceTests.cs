using ConduitLLM.WebUI.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Services;
using ConduitLLM.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class GlobalSettingServiceTests
    {
        private readonly Mock<IDbContextFactory<ConfigurationDbContext>> _mockConfigContextFactory;
        private readonly ILogger<GlobalSettingService> _logger;
        private readonly GlobalSettingService _service;
        
        // Legacy mocks kept for test compatibility
        private readonly Mock<IGlobalSettingRepository> _mockGlobalSettingRepository;

        public GlobalSettingServiceTests()
        {
            _mockConfigContextFactory = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            _logger = NullLogger<GlobalSettingService>.Instance;
            _mockGlobalSettingRepository = new Mock<IGlobalSettingRepository>();
            
            _service = new GlobalSettingService(
                _mockConfigContextFactory.Object,
                _logger);
        }

        [Fact]
        public async Task GetSettingAsync_ShouldReturnValue_WhenSettingExists()
        {
            // Arrange
            string key = "TestKey";
            string expectedValue = "TestValue";
            
            var mockContext = new Mock<ConfigurationDbContext>();
            var mockDbSet = new Mock<DbSet<GlobalSetting>>();
            
            var setting = new GlobalSetting { Key = key, Value = expectedValue };
            var settings = new List<GlobalSetting> { setting }.AsQueryable();
            
            mockDbSet.As<IQueryable<GlobalSetting>>().Setup(m => m.Provider).Returns(settings.Provider);
            mockDbSet.As<IQueryable<GlobalSetting>>().Setup(m => m.Expression).Returns(settings.Expression);
            mockDbSet.As<IQueryable<GlobalSetting>>().Setup(m => m.ElementType).Returns(settings.ElementType);
            mockDbSet.As<IQueryable<GlobalSetting>>().Setup(m => m.GetEnumerator()).Returns(settings.GetEnumerator());
            
            mockContext.Setup(c => c.GlobalSettings).Returns(mockDbSet.Object);
            
            _mockConfigContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockContext.Object);

            // Act
            var result = await _service.GetSettingAsync(key);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public async Task GetSettingAsync_ShouldReturnNull_WhenSettingDoesNotExist()
        {
            // Arrange
            string key = "NonExistentKey";
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                key, 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalSetting?)null);

            // Act
            var result = await _service.GetSettingAsync(key);

            // Assert
            Assert.Null(result);
            
            _mockGlobalSettingRepository.Verify(repo => repo.GetByKeyAsync(
                key, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task SetSettingAsync_ShouldCallRepositoryUpsert()
        {
            // Arrange
            string key = "TestKey";
            string value = "TestValue";
            
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                key, 
                value,
                null,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.SetSettingAsync(key, value);

            // Assert
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                key, 
                value,
                null,
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetMasterKeyHashAsync_ShouldReturnHash()
        {
            // Arrange
            string expectedHash = "abc123hash";
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "MasterKeyHash", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting 
                { 
                    Key = "MasterKeyHash", 
                    Value = expectedHash 
                });

            // Act
            var result = await _service.GetMasterKeyHashAsync();

            // Assert
            Assert.Equal(expectedHash, result);
            
            _mockGlobalSettingRepository.Verify(repo => repo.GetByKeyAsync(
                "MasterKeyHash", 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetMasterKeyHashAlgorithmAsync_ShouldReturnAlgorithm()
        {
            // Arrange
            string expectedAlgorithm = "SHA512";
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "MasterKeyHashAlgorithm", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting 
                { 
                    Key = "MasterKeyHashAlgorithm", 
                    Value = expectedAlgorithm 
                });

            // Act
            var result = await _service.GetMasterKeyHashAlgorithmAsync();

            // Assert
            Assert.Equal(expectedAlgorithm, result);
            
            _mockGlobalSettingRepository.Verify(repo => repo.GetByKeyAsync(
                "MasterKeyHashAlgorithm", 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetMasterKeyHashAlgorithmAsync_ShouldReturnDefaultSHA256_WhenNotSet()
        {
            // Arrange
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                "MasterKeyHashAlgorithm", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlobalSetting?)null);

            // Act
            var result = await _service.GetMasterKeyHashAlgorithmAsync();

            // Assert
            Assert.Equal("SHA256", result);
            
            _mockGlobalSettingRepository.Verify(repo => repo.GetByKeyAsync(
                "MasterKeyHashAlgorithm", 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task SetMasterKeyAsync_ShouldHashAndSaveKey()
        {
            // Arrange
            string masterKey = "SecureMasterKey123";
            
            // Simulate successful repository operations
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.SetMasterKeyAsync(masterKey);

            // Assert - verify both the hash and algorithm are saved
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                "MasterKeyHash",
                It.IsAny<string>(), // We can't predict the exact hash value, so we accept any string
                null,
                It.IsAny<CancellationToken>()), 
                Times.Once);
            
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                "MasterKeyHashAlgorithm",
                "SHA256",
                null,
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task SetMasterKeyAsync_ShouldThrowException_WhenKeyIsEmpty()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SetMasterKeyAsync(""));
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SetMasterKeyAsync(null!));
            
            // Verify repository was never called
            _mockGlobalSettingRepository.Verify(repo => repo.UpsertAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()), 
                Times.Never);
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
            
            // Setup repository capture
            string? capturedHash = null;
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                    "MasterKeyHash",
                    It.IsAny<string>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, string?, CancellationToken>((key, value, desc, token) => 
                {
                    capturedHash = value;
                })
                .ReturnsAsync(true);
                
            _mockGlobalSettingRepository.Setup(repo => repo.UpsertAsync(
                    "MasterKeyHashAlgorithm",
                    It.IsAny<string>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.SetMasterKeyAsync(masterKey);

            // Assert
            Assert.NotNull(capturedHash);
            Assert.Equal(expectedHash, capturedHash);
        }
    }
}