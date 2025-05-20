using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace ConduitLLM.Admin.Tests.Services
{
    public class AdminVirtualKeyServiceTests
    {
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly Mock<IVirtualKeySpendHistoryRepository> _mockSpendHistoryRepository;
        private readonly Mock<ILogger<AdminVirtualKeyService>> _mockLogger;
        private readonly AdminVirtualKeyService _service;

        public AdminVirtualKeyServiceTests()
        {
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockSpendHistoryRepository = new Mock<IVirtualKeySpendHistoryRepository>();
            _mockLogger = new Mock<ILogger<AdminVirtualKeyService>>();
            _service = new AdminVirtualKeyService(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_ValidRequest_ReturnsSuccessfulResponse()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly"
            };

            // Setup mock to return a new ID when creating a key
            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Setup mock to return a VirtualKey when GetByIdAsync is called
            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Test Key",
                    AllowedModels = "gpt-4",
                    MaxBudget = 100,
                    BudgetDuration = "monthly",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VirtualKey);
            Assert.NotNull(result.KeyInfo);
            Assert.Equal(1, result.KeyInfo.Id);
            Assert.Equal("Test Key", result.KeyInfo.KeyName);
            Assert.Equal("gpt-4", result.KeyInfo.AllowedModels);
            Assert.Equal(100, result.KeyInfo.MaxBudget);
            Assert.Equal("monthly", result.KeyInfo.BudgetDuration);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_ExistingId_ReturnsKey()
        {
            // Arrange
            int keyId = 1;
            var virtualKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.GetVirtualKeyInfoAsync(keyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(keyId, result.Id);
            Assert.Equal("Test Key", result.KeyName);
            Assert.Equal("gpt-4", result.AllowedModels);
            Assert.Equal(100, result.MaxBudget);
            Assert.Equal("monthly", result.BudgetDuration);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_NonExistingId_ReturnsNull()
        {
            // Arrange
            int keyId = 999;

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _service.GetVirtualKeyInfoAsync(keyId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_ReturnsAllKeys()
        {
            // Arrange
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey
                {
                    Id = 1,
                    KeyName = "Key 1",
                    AllowedModels = "gpt-4",
                    MaxBudget = 100,
                    BudgetDuration = "monthly",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new VirtualKey
                {
                    Id = 2,
                    KeyName = "Key 2",
                    AllowedModels = "gpt-3.5-turbo",
                    MaxBudget = 50,
                    BudgetDuration = "daily",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKeys);

            // Act
            var result = await _service.ListVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Id);
            Assert.Equal("Key 1", result[0].KeyName);
            Assert.Equal("gpt-4", result[0].AllowedModels);
            Assert.Equal(2, result[1].Id);
            Assert.Equal("Key 2", result[1].KeyName);
            Assert.Equal("gpt-3.5-turbo", result[1].AllowedModels);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_ExistingId_UpdatesAndReturnsTrue()
        {
            // Arrange
            int keyId = 1;
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Key",
                AllowedModels = "gpt-4,gpt-3.5-turbo",
                MaxBudget = 200
            };

            var virtualKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            _mockVirtualKeyRepository
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);

            // Assert
            Assert.True(result);
            
            // Verify that the repository's UpdateAsync was called with updated values
            _mockVirtualKeyRepository.Verify(r => r.UpdateAsync(
                It.Is<VirtualKey>(k => 
                    k.Id == keyId &&
                    k.KeyName == updateRequest.KeyName &&
                    k.AllowedModels == updateRequest.AllowedModels &&
                    k.MaxBudget == updateRequest.MaxBudget),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_NonExistingId_ReturnsFalse()
        {
            // Arrange
            int keyId = 999;
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Key",
                AllowedModels = "gpt-4,gpt-3.5-turbo",
                MaxBudget = 200
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);

            // Assert
            Assert.False(result);
            
            // Verify that the repository's UpdateAsync was not called
            _mockVirtualKeyRepository.Verify(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_ExistingId_DeletesAndReturnsTrue()
        {
            // Arrange
            int keyId = 1;

            _mockVirtualKeyRepository
                .Setup(r => r.DeleteAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteVirtualKeyAsync(keyId);

            // Assert
            Assert.True(result);
            
            // Verify that the repository's DeleteAsync was called with the correct ID
            _mockVirtualKeyRepository.Verify(r => r.DeleteAsync(keyId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_NonExistingId_ReturnsFalse()
        {
            // Arrange
            int keyId = 999;

            _mockVirtualKeyRepository
                .Setup(r => r.DeleteAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteVirtualKeyAsync(keyId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ResetSpendAsync_ExistingId_ResetsSpendAndReturnsTrue()
        {
            // Arrange
            int keyId = 1;
            var virtualKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                CurrentSpend = 50,
                BudgetDuration = "monthly",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            _mockSpendHistoryRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _mockVirtualKeyRepository
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ResetSpendAsync(keyId);

            // Assert
            Assert.True(result);
            
            // Verify history was created with correct amount
            _mockSpendHistoryRepository.Verify(r => r.CreateAsync(
                It.Is<VirtualKeySpendHistory>(h => 
                    h.VirtualKeyId == keyId &&
                    h.Amount == 50),
                It.IsAny<CancellationToken>()), 
                Times.Once);
            
            // Verify key was updated with spend reset to 0
            _mockVirtualKeyRepository.Verify(r => r.UpdateAsync(
                It.Is<VirtualKey>(k => 
                    k.Id == keyId &&
                    k.CurrentSpend == 0),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task ResetSpendAsync_NonExistingId_ReturnsFalse()
        {
            // Arrange
            int keyId = 999;

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _service.ResetSpendAsync(keyId);

            // Assert
            Assert.False(result);
            
            // Verify repository methods were not called
            _mockSpendHistoryRepository.Verify(r => r.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockVirtualKeyRepository.Verify(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}