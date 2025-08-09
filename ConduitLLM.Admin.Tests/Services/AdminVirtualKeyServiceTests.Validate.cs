using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        #region ValidateVirtualKeyAsync Tests

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


        [Fact]
        public async Task ValidateVirtualKeyAsync_ModelNotAllowed_ReturnsInvalidWithError()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                AllowedModels = "gpt-3.5-turbo,gpt-4",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, "claude-3-opus");

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Model claude-3-opus is not allowed for this key", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ValidKeyWithAllowedModel_ReturnsValid()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                AllowedModels = "gpt-3.5-turbo,gpt-4,claude-3-opus",
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 50m, // Has balance
                LifetimeCreditsAdded = 100m,
                LifetimeSpent = 50m
            };
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, "gpt-4");

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, result.VirtualKeyId);
            Assert.Equal("Test Key", result.KeyName);
            Assert.Equal("gpt-3.5-turbo,gpt-4,claude-3-opus", result.AllowedModels);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithoutVirtualKeyGroupId_ThrowsException()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key"
                // VirtualKeyGroupId is not set (will be 0 by default)
            };

            // Act & Assert
            // This test verifies that the DTO validation will catch the missing required field
            // In practice, the model validation in the controller will prevent this from reaching the service
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GenerateVirtualKeyAsync(request));
            
            Assert.Contains("Virtual key group 0 not found", exception.Message);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithNonExistentGroupId_ThrowsException()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 999
            };

            _mockGroupRepository.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((VirtualKeyGroup)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GenerateVirtualKeyAsync(request));
            
            Assert.Equal("Virtual key group 999 not found. Ensure the group exists before creating keys.", exception.Message);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithZeroBalanceGroup_LogsWarning()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 1
            };

            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 0m, // Zero balance
                LifetimeCreditsAdded = 0m,
                LifetimeSpent = 0m
            };

            _mockGroupRepository.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(group);
            _mockVirtualKeyRepository.Setup(x => x.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey { Id = 1, KeyName = "Test Key", VirtualKeyGroupId = 1 });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Virtual key group 1 has zero balance")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithValidGroup_CreatesKeySuccessfully()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key",
                VirtualKeyGroupId = 1,
                AllowedModels = "gpt-4",
                RateLimitRpm = 100
            };

            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 1000m,
                LifetimeCreditsAdded = 1000m,
                LifetimeSpent = 0m
            };

            _mockGroupRepository.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(group);
            _mockVirtualKeyRepository.Setup(x => x.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Test Key", 
                    VirtualKeyGroupId = 1,
                    AllowedModels = "gpt-4",
                    RateLimitRpm = 100
                });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VirtualKey);
            Assert.Equal("Test Key", result.KeyInfo.KeyName);
            Assert.Equal(1, result.KeyInfo.VirtualKeyGroupId);
            
            // Verify no warning about zero balance
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("zero balance")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ValidKeyWithWildcardModel_ReturnsValid()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                AllowedModels = "gpt-4*,claude-*",
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            var group = new VirtualKeyGroup { Id = 1, Balance = 75m };
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, "gpt-4-turbo-preview");

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ValidKeyNoModelRestriction_ReturnsValid()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                AllowedModels = "", // No restrictions
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            var group = new VirtualKeyGroup { Id = 1, Balance = 100m };
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, "any-model");

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, result.VirtualKeyId);
        }


        [Fact]
        public async Task ValidateVirtualKeyAsync_ValidKeyNoExpiration_ReturnsValid()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                ExpiresAt = null, // No expiration
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow.AddYears(-1), // Old key but no expiration
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            var group = new VirtualKeyGroup { Id = 1, Balance = 1000m };
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ModelWithSpacesAndCase_HandlesCorrectly()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                AllowedModels = " GPT-4 , Claude-3-Opus , GPT-3.5-Turbo ", // Spaces and mixed case
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            var group = new VirtualKeyGroup { Id = 1, Balance = 500m };
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, "gpt-4"); // lowercase

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ComplexWildcardPattern_HandlesCorrectly()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                AllowedModels = "gpt-4*,claude-3-*,text-embedding-*",
                VirtualKeyGroupId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);
            
            var group = new VirtualKeyGroup { Id = 1, Balance = 250m };
            _mockGroupRepository.Setup(x => x.GetByKeyIdAsync(1))
                .ReturnsAsync(group);

            // Act & Assert - Multiple model tests
            var result1 = await _service.ValidateVirtualKeyAsync(key, "claude-3-opus-20240229");
            Assert.True(result1.IsValid);

            var result2 = await _service.ValidateVirtualKeyAsync(key, "text-embedding-ada-002");
            Assert.True(result2.IsValid);

            var result3 = await _service.ValidateVirtualKeyAsync(key, "gpt-3.5-turbo");
            Assert.False(result3.IsValid);
            Assert.Equal("Model gpt-3.5-turbo is not allowed for this key", result3.ErrorMessage);
        }

        #endregion
    }
}