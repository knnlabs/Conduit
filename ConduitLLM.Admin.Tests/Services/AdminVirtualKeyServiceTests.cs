using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
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
    public class AdminVirtualKeyServiceTests
    {
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly Mock<IVirtualKeySpendHistoryRepository> _mockSpendHistoryRepository;
        private readonly Mock<IVirtualKeyCache> _mockCache;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<AdminVirtualKeyService>> _mockLogger;
        private readonly Mock<IMediaLifecycleService> _mockMediaLifecycleService;
        private readonly Mock<IModelProviderMappingRepository> _mockModelProviderMappingRepository;
        private readonly Mock<IModelCapabilityService> _mockModelCapabilityService;
        private readonly AdminVirtualKeyService _service;

        public AdminVirtualKeyServiceTests()
        {
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockSpendHistoryRepository = new Mock<IVirtualKeySpendHistoryRepository>();
            _mockCache = new Mock<IVirtualKeyCache>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<AdminVirtualKeyService>>();
            _mockMediaLifecycleService = new Mock<IMediaLifecycleService>();
            _mockModelProviderMappingRepository = new Mock<IModelProviderMappingRepository>();
            _mockModelCapabilityService = new Mock<IModelCapabilityService>();

            _service = new AdminVirtualKeyService(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _mockCache.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object,
                _mockModelProviderMappingRepository.Object,
                _mockModelCapabilityService.Object,
                _mockMediaLifecycleService.Object);
        }

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
        public async Task ValidateVirtualKeyAsync_BudgetDepleted_ReturnsInvalidWithError()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                MaxBudget = 100m,
                CurrentSpend = 100m, // Budget fully spent
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("Budget depleted", result.ErrorMessage);
            Assert.Null(result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_BudgetExceeded_ReturnsInvalidWithError()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                MaxBudget = 100m,
                CurrentSpend = 150m, // Over budget
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

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
                MaxBudget = 100m,
                CurrentSpend = 50m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, "gpt-4");

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, result.VirtualKeyId);
            Assert.Equal("Test Key", result.KeyName);
            Assert.Equal("gpt-3.5-turbo,gpt-4,claude-3-opus", result.AllowedModels);
            Assert.Equal(100m, result.MaxBudget);
            Assert.Equal(50m, result.CurrentSpend);
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
                MaxBudget = 100m,
                CurrentSpend = 25m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key, "any-model");

            // Assert
            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, result.VirtualKeyId);
        }

        [Fact]
        public async Task ValidateVirtualKeyAsync_ValidKeyNoBudgetLimit_ReturnsValid()
        {
            // Arrange
            var key = VirtualKeyConstants.KeyPrefix + "testkey123";
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123",
                IsEnabled = true,
                MaxBudget = null, // No budget limit
                CurrentSpend = 10000m, // High spend but no limit
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            // Act
            var result = await _service.ValidateVirtualKeyAsync(key);

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
                CreatedAt = DateTime.UtcNow.AddYears(-1), // Old key but no expiration
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

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

        #region GenerateVirtualKeyAsync Tests

        [Fact]
        public async Task GenerateVirtualKeyAsync_ValidRequest_CreatesKeySuccessfully()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test API Key",
                MaxBudget = 100m,
                AllowedModels = "gpt-4,claude-3",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                BudgetDuration = VirtualKeyConstants.BudgetPeriods.Monthly,
                RateLimitRpm = 60,
                RateLimitRpd = 1000
            };

            _mockVirtualKeyRepository.Setup(x => x.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = 1,
                    KeyName = request.KeyName,
                    KeyHash = "someHash",
                    IsEnabled = true,
                    MaxBudget = request.MaxBudget,
                    AllowedModels = request.AllowedModels,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.VirtualKey);
            Assert.StartsWith(VirtualKeyConstants.KeyPrefix, result.VirtualKey);
            Assert.NotNull(result.KeyInfo);
            Assert.Equal(1, result.KeyInfo.Id);
            Assert.Equal("Test API Key", result.KeyInfo.KeyName);

            // Verify event was published
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<VirtualKeyCreated>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region UpdateVirtualKeyAsync Tests

        [Fact]
        public async Task UpdateVirtualKeyAsync_KeyNotFound_ReturnsFalse()
        {
            // Arrange
            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey?)null);

            var request = new UpdateVirtualKeyRequestDto { KeyName = "Updated Name" };

            // Act
            var result = await _service.UpdateVirtualKeyAsync(999, request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_NoChanges_ReturnsTrue()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                IsEnabled = true,
                AllowedModels = "gpt-4"
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            var request = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Test Key", // Same name
                IsEnabled = true, // Same status
                AllowedModels = "gpt-4" // Same models
            };

            // Act
            var result = await _service.UpdateVirtualKeyAsync(1, request);

            // Assert
            Assert.True(result);
            _mockVirtualKeyRepository.Verify(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_WithChanges_UpdatesAndPublishesEvent()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Old Name",
                IsEnabled = true,
                AllowedModels = "gpt-3.5-turbo"
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new UpdateVirtualKeyRequestDto
            {
                KeyName = "New Name",
                IsEnabled = false,
                AllowedModels = "gpt-4"
            };

            // Act
            var result = await _service.UpdateVirtualKeyAsync(1, request);

            // Assert
            Assert.True(result);
            _mockVirtualKeyRepository.Verify(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<VirtualKeyUpdated>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region DeleteVirtualKeyAsync Tests

        [Fact]
        public async Task DeleteVirtualKeyAsync_KeyNotFound_ReturnsFalse()
        {
            // Arrange
            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            var result = await _service.DeleteVirtualKeyAsync(999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_ValidKey_DeletesAndPublishesEvent()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123"
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockMediaLifecycleService.Setup(x => x.DeleteMediaForVirtualKeyAsync(1))
                .ReturnsAsync(5); // 5 media files deleted

            // Act
            var result = await _service.DeleteVirtualKeyAsync(1);

            // Assert
            Assert.True(result);
            _mockVirtualKeyRepository.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
            _mockMediaLifecycleService.Verify(x => x.DeleteMediaForVirtualKeyAsync(1), Times.Once);
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<VirtualKeyDeleted>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_MediaCleanupFails_StillDeletesKey()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                KeyName = "Test Key",
                KeyHash = "hash123"
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockMediaLifecycleService.Setup(x => x.DeleteMediaForVirtualKeyAsync(1))
                .ThrowsAsync(new Exception("Media service error"));

            // Act
            var result = await _service.DeleteVirtualKeyAsync(1);

            // Assert
            Assert.True(result); // Key deletion should succeed despite media cleanup failure
            _mockVirtualKeyRepository.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Budget and Spend Tests

        [Fact]
        public async Task ResetSpendAsync_ValidKey_ResetsSpendAndInvalidatesCache()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash123",
                CurrentSpend = 50m,
                BudgetDuration = VirtualKeyConstants.BudgetPeriods.Monthly
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ResetSpendAsync(1);

            // Assert
            Assert.True(result);
            Assert.Equal(0m, existingKey.CurrentSpend);
            _mockSpendHistoryRepository.Verify(x => x.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCache.Verify(x => x.InvalidateVirtualKeyAsync("hash123"), Times.Once);
        }

        [Fact]
        public async Task UpdateSpendAsync_ValidKey_UpdatesSpendAndInvalidatesCache()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash123",
                CurrentSpend = 50m
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateSpendAsync(1, 25m);

            // Assert
            Assert.True(result);
            Assert.Equal(75m, existingKey.CurrentSpend);
            _mockCache.Verify(x => x.InvalidateVirtualKeyAsync("hash123"), Times.Once);
        }

        [Fact]
        public async Task CheckBudgetAsync_MonthlyBudgetExpired_ResetsSpend()
        {
            // Arrange
            var lastMonth = DateTime.UtcNow.AddMonths(-1);
            var existingKey = new VirtualKey
            {
                Id = 1,
                CurrentSpend = 100m,
                BudgetDuration = VirtualKeyConstants.BudgetPeriods.Monthly,
                BudgetStartDate = new DateTime(lastMonth.Year, lastMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CheckBudgetAsync(1);

            // Assert
            Assert.True(result.WasReset);
            Assert.NotNull(result.NewBudgetStartDate);
            Assert.Equal(0m, existingKey.CurrentSpend);
            _mockSpendHistoryRepository.Verify(x => x.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckBudgetAsync_DailyBudgetExpired_ResetsSpend()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                CurrentSpend = 50m,
                BudgetDuration = VirtualKeyConstants.BudgetPeriods.Daily,
                BudgetStartDate = DateTime.UtcNow.AddDays(-1).Date
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CheckBudgetAsync(1);

            // Assert
            Assert.True(result.WasReset);
            Assert.NotNull(result.NewBudgetStartDate);
            Assert.Equal(DateTime.UtcNow.Date, result.NewBudgetStartDate.Value.Date);
            Assert.Equal(0m, existingKey.CurrentSpend);
        }

        [Fact]
        public async Task CheckBudgetAsync_TotalBudget_NeverResets()
        {
            // Arrange
            var existingKey = new VirtualKey
            {
                Id = 1,
                CurrentSpend = 500m,
                BudgetDuration = VirtualKeyConstants.BudgetPeriods.Total,
                BudgetStartDate = DateTime.UtcNow.AddYears(-1)
            };

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            // Act
            var result = await _service.CheckBudgetAsync(1);

            // Assert
            Assert.False(result.WasReset);
            Assert.Null(result.NewBudgetStartDate);
            _mockVirtualKeyRepository.Verify(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region PerformMaintenanceAsync Tests

        [Fact]
        public async Task PerformMaintenanceAsync_ProcessesExpiredKeysAndBudgets()
        {
            // Arrange
            var keys = new List<VirtualKey>
            {
                // Expired key that should be disabled
                new VirtualKey
                {
                    Id = 1,
                    KeyName = "Expired Key",
                    IsEnabled = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(-1)
                },
                // Key with monthly budget that needs reset
                new VirtualKey
                {
                    Id = 2,
                    KeyName = "Monthly Budget Key",
                    IsEnabled = true,
                    CurrentSpend = 100m,
                    BudgetDuration = VirtualKeyConstants.BudgetPeriods.Monthly,
                    BudgetStartDate = DateTime.UtcNow.AddMonths(-1).Date
                },
                // Valid key that shouldn't change
                new VirtualKey
                {
                    Id = 3,
                    KeyName = "Valid Key",
                    IsEnabled = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(30)
                }
            };

            _mockVirtualKeyRepository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(keys);

            _mockVirtualKeyRepository.Setup(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockVirtualKeyRepository.Setup(x => x.GetByIdAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(keys[1]);

            // Act
            await _service.PerformMaintenanceAsync();

            // Assert
            // Verify expired key was disabled
            Assert.False(keys[0].IsEnabled);
            
            // Verify budget was reset
            _mockSpendHistoryRepository.Verify(x => x.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()), Times.Once);
            
            // Verify updates were called
            _mockVirtualKeyRepository.Verify(x => x.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        }

        #endregion
    }
}