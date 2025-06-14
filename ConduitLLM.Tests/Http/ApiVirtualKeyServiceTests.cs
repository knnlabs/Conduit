using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Http.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Http
{
    public class ApiVirtualKeyServiceTests
    {
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly Mock<IVirtualKeySpendHistoryRepository> _mockSpendHistoryRepository;
        private readonly Mock<ILogger<ApiVirtualKeyService>> _mockLogger;
        private readonly ApiVirtualKeyService _service;

        public ApiVirtualKeyServiceTests()
        {
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockSpendHistoryRepository = new Mock<IVirtualKeySpendHistoryRepository>();
            _mockLogger = new Mock<ILogger<ApiVirtualKeyService>>();
            _service = new ApiVirtualKeyService(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_Should_Create_New_Key()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                AllowedModels = "gpt-4,gpt-3.5-turbo",
                MaxBudget = 100m,
                BudgetDuration = "Monthly",
                RateLimitRpm = 60
            };

            var createdKey = new VirtualKey
            {
                Id = 1,
                KeyName = request.KeyName,
                KeyHash = "hashed_value",
                AllowedModels = request.AllowedModels,
                MaxBudget = request.MaxBudget,
                CurrentSpend = 0,
                BudgetDuration = request.BudgetDuration,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.CreateAsync(It.IsAny<VirtualKey>()))
                .ReturnsAsync(createdKey);

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VirtualKey);
            Assert.StartsWith("condt_", result.VirtualKey);
            Assert.NotNull(result.KeyInfo);
            Assert.Equal(request.KeyName, result.KeyInfo.KeyName);
            Assert.Equal(request.AllowedModels, result.KeyInfo.AllowedModels);
            Assert.Equal(request.MaxBudget, result.KeyInfo.MaxBudget);
        }

        [Fact]
        public async Task GetVirtualKeyInfoAsync_Should_Return_Key_Info()
        {
            // Arrange
            var keyId = 1;
            var virtualKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                KeyHash = "hashed_value",
                IsEnabled = true,
                CurrentSpend = 50m,
                MaxBudget = 100m
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(keyId, default))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.GetVirtualKeyInfoAsync(keyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(keyId, result.Id);
            Assert.Equal("Test Key", result.KeyName);
            Assert.Equal(50m, result.CurrentSpend);
            Assert.Equal(100m, result.MaxBudget);
            Assert.Equal("condt_****", result.KeyPrefix);
        }

        [Fact]
        public async Task ListVirtualKeysAsync_Should_Return_All_Keys()
        {
            // Arrange
            var virtualKeys = new List<VirtualKey>
            {
                new VirtualKey { Id = 1, KeyName = "Key 1", IsEnabled = true },
                new VirtualKey { Id = 2, KeyName = "Key 2", IsEnabled = false }
            };

            _mockVirtualKeyRepository.Setup(x => x.GetAllAsync(default))
                .ReturnsAsync(virtualKeys);

            // Act
            var result = await _service.ListVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Key 1", result[0].KeyName);
            Assert.Equal("Key 2", result[1].KeyName);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_Should_Update_Existing_Key()
        {
            // Arrange
            var keyId = 1;
            var existingKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Old Name",
                IsEnabled = true,
                MaxBudget = 50m
            };

            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                KeyName = "New Name",
                MaxBudget = 100m,
                IsEnabled = false
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(keyId, default))
                .ReturnsAsync(existingKey);
            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);

            // Assert
            Assert.True(result);
            _mockVirtualKeyRepository.Verify(x => x.UpdateAsync(
                It.Is<VirtualKey>(k => 
                    k.KeyName == "New Name" && 
                    k.MaxBudget == 100m && 
                    k.IsEnabled == false), 
                default), Times.Once);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_Should_Delete_Key()
        {
            // Arrange
            var keyId = 1;
            var virtualKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key"
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(keyId, default))
                .ReturnsAsync(virtualKey);
            _mockVirtualKeyRepository.Setup(x => x.DeleteAsync(keyId, default))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteVirtualKeyAsync(keyId);

            // Assert
            Assert.True(result);
            _mockVirtualKeyRepository.Verify(x => x.DeleteAsync(keyId, default), Times.Once);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_Should_Validate_Model_Restrictions()
        {
            // Arrange
            var keyHash = "test_hash";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = keyHash,
                IsEnabled = true,
                AllowedModels = "gpt-4*,claude-3"
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(keyHash))
                .ReturnsAsync(virtualKey);

            // Act - Test allowed model with wildcard
            var result1 = await _service.ValidateVirtualKeyAsync(keyHash, "gpt-4-turbo");
            Assert.NotNull(result1);

            // Act - Test exact match
            var result2 = await _service.ValidateVirtualKeyAsync(keyHash, "claude-3");
            Assert.NotNull(result2);

            // Act - Test restricted model
            var result3 = await _service.ValidateVirtualKeyAsync(keyHash, "gpt-3.5-turbo");
            Assert.Null(result3);
        }

        [Fact]
        public async Task UpdateSpendAsync_Should_Increment_Spend()
        {
            // Arrange
            var keyId = 1;
            var virtualKey = new VirtualKey
            {
                Id = keyId,
                CurrentSpend = 50m
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(keyId, default))
                .ReturnsAsync(virtualKey);
            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateSpendAsync(keyId, 25m);

            // Assert
            Assert.True(result);
            _mockVirtualKeyRepository.Verify(x => x.UpdateAsync(
                It.Is<VirtualKey>(k => k.CurrentSpend == 75m), 
                default), Times.Once);
        }
    }
}