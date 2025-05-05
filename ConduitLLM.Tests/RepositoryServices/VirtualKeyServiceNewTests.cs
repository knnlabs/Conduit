using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class VirtualKeyServiceNewTests
    {
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly Mock<IVirtualKeySpendHistoryRepository> _mockSpendHistoryRepository;
        private readonly ILogger<VirtualKeyServiceNew> _logger;
        private readonly VirtualKeyServiceNew _service;

        public VirtualKeyServiceNewTests()
        {
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockSpendHistoryRepository = new Mock<IVirtualKeySpendHistoryRepository>();
            _logger = NullLogger<VirtualKeyServiceNew>.Instance;
            _service = new VirtualKeyServiceNew(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _logger);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_ShouldCreateKeyAndReturnResponse()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m,
                BudgetDuration = "monthly",
                Metadata = "Test metadata"
            };

            var createdEntityId = 123;
            
            _mockVirtualKeyRepository.Setup(repo => repo.CreateAsync(
                It.IsAny<VirtualKey>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdEntityId);

            _mockVirtualKeyRepository.Setup(repo => repo.GetByIdAsync(
                createdEntityId,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = createdEntityId,
                    KeyName = request.KeyName,
                    AllowedModels = request.AllowedModels,
                    MaxBudget = request.MaxBudget,
                    BudgetDuration = request.BudgetDuration,
                    BudgetStartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsEnabled = true,
                    Metadata = request.Metadata,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VirtualKey);
            Assert.NotNull(result.KeyInfo);
            Assert.StartsWith(VirtualKeyConstants.KeyPrefix, result.VirtualKey);
            Assert.Equal(createdEntityId, result.KeyInfo.Id);
            Assert.Equal(request.KeyName, result.KeyInfo.KeyName);
            Assert.Equal(request.AllowedModels, result.KeyInfo.AllowedModels);
            Assert.Equal(request.MaxBudget, result.KeyInfo.MaxBudget);
            Assert.Equal(request.BudgetDuration, result.KeyInfo.BudgetDuration);
            Assert.Equal(request.Metadata, result.KeyInfo.Metadata);
            
            _mockVirtualKeyRepository.Verify(repo => repo.CreateAsync(
                It.IsAny<VirtualKey>(), 
                It.IsAny<CancellationToken>()), 
                Times.Once);
            
            _mockVirtualKeyRepository.Verify(repo => repo.GetByIdAsync(
                createdEntityId, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_ShouldReturnMappedDto_WhenKeyExists()
        {
            // Arrange
            int keyId = 123;
            var virtualKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100.0m,
                CurrentSpend = 25.0m,
                BudgetDuration = "monthly",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.GetVirtualKeyInfoAsync(keyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(keyId, result.Id);
            Assert.Equal(virtualKey.KeyName, result.KeyName);
            Assert.Equal(virtualKey.AllowedModels, result.AllowedModels);
            Assert.Equal(virtualKey.MaxBudget, result.MaxBudget);
            Assert.Equal(virtualKey.CurrentSpend, result.CurrentSpend);
            Assert.Equal(virtualKey.BudgetDuration, result.BudgetDuration);
            Assert.Equal(virtualKey.IsEnabled, result.IsEnabled);
            
            _mockVirtualKeyRepository.Verify(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_ShouldReturnNull_WhenKeyDoesNotExist()
        {
            // Arrange
            int keyId = 999;
            
            _mockVirtualKeyRepository.Setup(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            var result = await _service.GetVirtualKeyInfoAsync(keyId);

            // Assert
            Assert.Null(result);
            
            _mockVirtualKeyRepository.Verify(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_ShouldReturnAllKeys()
        {
            // Arrange
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey
                {
                    Id = 1,
                    KeyName = "Key 1",
                    IsEnabled = true
                },
                new VirtualKey
                {
                    Id = 2,
                    KeyName = "Key 2",
                    IsEnabled = false
                }
            };

            _mockVirtualKeyRepository.Setup(repo => repo.GetAllAsync(
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKeys);

            // Act
            var results = await _service.ListVirtualKeysAsync();

            // Assert
            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
            Assert.Equal(1, results[0].Id);
            Assert.Equal("Key 1", results[0].KeyName);
            Assert.True(results[0].IsEnabled);
            Assert.Equal(2, results[1].Id);
            Assert.Equal("Key 2", results[1].KeyName);
            Assert.False(results[1].IsEnabled);
            
            _mockVirtualKeyRepository.Verify(repo => repo.GetAllAsync(
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_ShouldUpdateExistingKey_ReturnTrue()
        {
            // Arrange
            int keyId = 123;
            var existingKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Original Name",
                AllowedModels = "gpt-4",
                MaxBudget = 100.0m,
                BudgetDuration = "monthly",
                IsEnabled = true,
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var request = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Name",
                AllowedModels = "gpt-4,claude-2",
                MaxBudget = 200.0m,
                IsEnabled = false
            };

            _mockVirtualKeyRepository.Setup(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(repo => repo.UpdateAsync(
                It.IsAny<VirtualKey>(), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, request);

            // Assert
            Assert.True(result);
            
            _mockVirtualKeyRepository.Verify(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
            
            _mockVirtualKeyRepository.Verify(repo => repo.UpdateAsync(
                It.Is<VirtualKey>(vk => 
                    vk.Id == keyId &&
                    vk.KeyName == request.KeyName &&
                    vk.AllowedModels == request.AllowedModels &&
                    vk.MaxBudget == request.MaxBudget &&
                    vk.BudgetDuration == existingKey.BudgetDuration &&
                    vk.IsEnabled == request.IsEnabled),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ShouldReturnNull_WhenKeyInvalid()
        {
            // Arrange
            string invalidKey = "invalid-key";
            
            // Act
            var result = await _service.ValidateVirtualKeyAsync(invalidKey);
            
            // Assert
            Assert.Null(result);
            _mockVirtualKeyRepository.Verify(repo => repo.GetByKeyHashAsync(
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()), 
                Times.Never);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ShouldReturnVirtualKey_WhenKeyValid()
        {
            // Arrange
            string validKey = $"{VirtualKeyConstants.KeyPrefix}test-key-string";
            string keyHash = GetHashForTest(validKey);
            
            var virtualKey = new VirtualKey
            {
                Id = 123,
                KeyName = "Test Key",
                KeyHash = keyHash,
                IsEnabled = true,
                MaxBudget = 100.0m,
                CurrentSpend = 50.0m
            };
            
            _mockVirtualKeyRepository.Setup(repo => repo.GetByKeyHashAsync(
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            // Act
            var result = await _service.ValidateVirtualKeyAsync(validKey);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
            Assert.Equal("Test Key", result.KeyName);
            Assert.True(result.IsEnabled);
            
            _mockVirtualKeyRepository.Verify(repo => repo.GetByKeyHashAsync(
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task ResetSpendAsync_ShouldResetSpendToZero_AndAddSpendHistory()
        {
            // Arrange
            int keyId = 123;
            var virtualKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                CurrentSpend = 75.5m,
                BudgetDuration = "monthly"
            };
            
            _mockVirtualKeyRepository.Setup(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            _mockVirtualKeyRepository.Setup(repo => repo.UpdateAsync(
                It.IsAny<VirtualKey>(), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            _mockSpendHistoryRepository.Setup(repo => repo.CreateAsync(
                It.IsAny<VirtualKeySpendHistory>(), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            
            // Act
            var result = await _service.ResetSpendAsync(keyId);
            
            // Assert
            Assert.True(result);
            
            _mockVirtualKeyRepository.Verify(repo => repo.GetByIdAsync(
                keyId, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
            
            _mockSpendHistoryRepository.Verify(repo => repo.CreateAsync(
                It.Is<VirtualKeySpendHistory>(sh => 
                    sh.VirtualKeyId == keyId &&
                    sh.Amount == 75.5m),
                It.IsAny<CancellationToken>()), 
                Times.Once);
            
            _mockVirtualKeyRepository.Verify(repo => repo.UpdateAsync(
                It.Is<VirtualKey>(vk => 
                    vk.Id == keyId &&
                    vk.CurrentSpend == 0m),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        // Helper method to simulate the hash generation for testing
        private string GetHashForTest(string key)
        {
            using var sha256 = SHA256.Create();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] hashBytes = sha256.ComputeHash(keyBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}