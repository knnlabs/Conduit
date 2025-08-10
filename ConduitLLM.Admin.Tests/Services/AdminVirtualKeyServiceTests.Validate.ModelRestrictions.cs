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
        #region Model Restriction Tests

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