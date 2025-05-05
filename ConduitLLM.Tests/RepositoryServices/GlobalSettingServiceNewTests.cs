using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class GlobalSettingServiceNewTests
    {
        private readonly Mock<IGlobalSettingRepository> _mockGlobalSettingRepository;
        private readonly ILogger<GlobalSettingServiceNew> _logger;
        private readonly GlobalSettingServiceNew _service;

        public GlobalSettingServiceNewTests()
        {
            _mockGlobalSettingRepository = new Mock<IGlobalSettingRepository>();
            _logger = NullLogger<GlobalSettingServiceNew>.Instance;
            _service = new GlobalSettingServiceNew(
                _mockGlobalSettingRepository.Object,
                _logger);
        }

        [Fact]
        public async Task GetSettingAsync_ShouldReturnValue_WhenSettingExists()
        {
            // Arrange
            string key = "TestKey";
            string expectedValue = "TestValue";
            
            _mockGlobalSettingRepository.Setup(repo => repo.GetByKeyAsync(
                key, 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlobalSetting 
                { 
                    Key = key, 
                    Value = expectedValue 
                });

            // Act
            var result = await _service.GetSettingAsync(key);

            // Assert
            Assert.Equal(expectedValue, result);
            
            _mockGlobalSettingRepository.Verify(repo => repo.GetByKeyAsync(
                key, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
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