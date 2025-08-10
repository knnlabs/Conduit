using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        #region Basic Validation Tests

        [Fact]
        public async Task ValidateVirtualKeyAsync_EmptyKey_ReturnsInvalidWithError()
        {
            // Arrange
            var key = "";

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Key cannot be empty", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_NullKey_ReturnsInvalidWithError()
        {
            // Arrange
            string? key = null;

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key!);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Key cannot be empty", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_InvalidPrefix_ReturnsInvalidWithError()
        {
            // Arrange
            var key = "invalid_prefix_key123456789";

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Invalid key format: doesn't start with required prefix", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_KeyNotFound_ReturnsInvalidWithError()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "nonexistentkey123";
            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Key not found", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_DisabledKey_ReturnsInvalidWithError()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Key is disabled", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ExpiredKey_ReturnsInvalidWithError()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired yesterday
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Key has expired", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_GroupBudgetDepleted_ReturnsInvalidWithError()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 0m, // No balance left
                LifetimeCreditsAdded = 100m,
                LifetimeSpent = 100m
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Budget depleted", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        #endregion
    }
}