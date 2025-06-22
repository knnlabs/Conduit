using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

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
        private readonly AdminVirtualKeyService _service;

        public AdminVirtualKeyServiceTests()
        {
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockSpendHistoryRepository = new Mock<IVirtualKeySpendHistoryRepository>();
            _mockCache = new Mock<IVirtualKeyCache>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<AdminVirtualKeyService>>();
            
            // Default service setup with mocks
            _service = new AdminVirtualKeyService(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _mockCache.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object);
        }

        #region GenerateVirtualKeyAsync Tests

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
        public async Task GenerateVirtualKeyAsync_ValidRequest_PublishesVirtualKeyUpdatedEvent()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "New Virtual Key",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly",
                RateLimitRpm = 60,
                RateLimitRpd = 1000
            };

            var createdKey = new VirtualKey
            {
                Id = 1,
                KeyName = "New Virtual Key",
                KeyHash = "generated-hash-abc123",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                BudgetDuration = "monthly",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RateLimitRpm = 60,
                RateLimitRpd = 1000
            };

            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdKey);

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);

            // Verify that VirtualKeyUpdated event was published with "Created" marker
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<VirtualKeyUpdated>(e => 
                    e.KeyId == 1 &&
                    e.KeyHash == "generated-hash-abc123" &&
                    e.ChangedProperties.Length == 1 &&
                    e.ChangedProperties[0] == "Created"),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithoutPublishEndpoint_StillCreatesKey()
        {
            // Arrange - Create service without publish endpoint
            var service = new AdminVirtualKeyService(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _mockCache.Object,
                null, // No publish endpoint
                _mockLogger.Object);

            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key Without Events",
                AllowedModels = "gpt-3.5-turbo"
            };

            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = 2,
                    KeyName = "Test Key Without Events",
                    KeyHash = "test-hash",
                    AllowedModels = "gpt-3.5-turbo",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            // Act
            var result = await service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.KeyInfo.Id);
            Assert.Equal("Test Key Without Events", result.KeyInfo.KeyName);
            
            // Verify no events were published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.IsAny<VirtualKeyUpdated>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_EventPublishingFails_StillCreatesKey()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Test Key With Failed Event",
                MaxBudget = 50
            };

            var createdKey = new VirtualKey
            {
                Id = 3,
                KeyName = "Test Key With Failed Event",
                KeyHash = "test-hash-failed",
                MaxBudget = 50,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdKey);

            // Setup publish to throw exception
            _mockPublishEndpoint
                .Setup(p => p.Publish(It.IsAny<VirtualKeyUpdated>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Event bus error"));

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert - Key should still be created successfully
            Assert.NotNull(result);
            Assert.Equal(3, result.KeyInfo.Id);
            Assert.Equal("Test Key With Failed Event", result.KeyInfo.KeyName);

            // Verify warning was logged about failed event
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to publish VirtualKeyUpdated event")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_WithBudget_CreatesSpendHistory()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Key with Budget",
                MaxBudget = 200,
                BudgetDuration = "daily"
            };

            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(4);

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = 4,
                    KeyName = "Key with Budget",
                    KeyHash = "budget-key-hash",
                    MaxBudget = 200,
                    BudgetDuration = "daily",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            // Act
            var result = await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(result);

            // Verify spend history was created
            _mockSpendHistoryRepository.Verify(r => r.CreateAsync(
                It.Is<VirtualKeySpendHistory>(h => 
                    h.VirtualKeyId == 4 &&
                    h.Amount == 0),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify event was published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<VirtualKeyUpdated>(e => 
                    e.KeyId == 4 &&
                    e.KeyHash == "budget-key-hash"),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateVirtualKeyAsync_RepositoryCreateFails_ThrowsException()
        {
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Failed Key"
            };

            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            // Setup GetByIdAsync to return null (simulating failure to retrieve)
            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await _service.GenerateVirtualKeyAsync(request));

            // Verify no event was published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.IsAny<VirtualKeyUpdated>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

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
                .ReturnsAsync((VirtualKey?)null);

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

        #region UpdateVirtualKeyAsync Tests

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
        public async Task UpdateVirtualKeyAsync_ExistingId_PublishesVirtualKeyUpdatedEvent()
        {
            // Arrange
            int keyId = 1;
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Name",
                AllowedModels = "gpt-4,claude-3",
                MaxBudget = 300,
                IsEnabled = false,
                RateLimitRpm = 120
            };

            var existingKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Original Name",
                KeyHash = "key-hash-123",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                IsEnabled = true,
                RateLimitRpm = 60,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);

            // Assert
            Assert.True(result);

            // Verify event was published with correct changed properties
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<VirtualKeyUpdated>(e => 
                    e.KeyId == keyId &&
                    e.KeyHash == "key-hash-123" &&
                    e.ChangedProperties.Contains("KeyName") &&
                    e.ChangedProperties.Contains("AllowedModels") &&
                    e.ChangedProperties.Contains("MaxBudget") &&
                    e.ChangedProperties.Contains("IsEnabled") &&
                    e.ChangedProperties.Contains("RateLimitRpm") &&
                    e.ChangedProperties.Length == 5),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify cache was invalidated
            _mockCache.Verify(c => c.InvalidateVirtualKeyAsync("key-hash-123"), Times.Once);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_NoChanges_DoesNotPublishEvent()
        {
            // Arrange
            int keyId = 2;
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                KeyName = "Same Name",
                AllowedModels = "gpt-4",
                MaxBudget = 100
            };

            var existingKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Same Name",
                KeyHash = "key-hash-456",
                AllowedModels = "gpt-4",
                MaxBudget = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);

            // Assert
            Assert.True(result); // Should return true even though no changes

            // Verify no update was called
            _mockVirtualKeyRepository.Verify(r => r.UpdateAsync(
                It.IsAny<VirtualKey>(), 
                It.IsAny<CancellationToken>()), 
                Times.Never);

            // Verify no event was published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.IsAny<VirtualKeyUpdated>(),
                It.IsAny<CancellationToken>()),
                Times.Never);

            // Verify no cache invalidation
            _mockCache.Verify(c => c.InvalidateVirtualKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_EventPublishingFails_StillUpdatesKey()
        {
            // Arrange
            int keyId = 3;
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                IsEnabled = false
            };

            var existingKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                KeyHash = "key-hash-789",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Setup publish to throw exception
            _mockPublishEndpoint
                .Setup(p => p.Publish(It.IsAny<VirtualKeyUpdated>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Event bus error"));

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);

            // Assert - Update should still succeed
            Assert.True(result);

            // Verify update was called
            _mockVirtualKeyRepository.Verify(r => r.UpdateAsync(
                It.Is<VirtualKey>(k => k.IsEnabled == false),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to publish VirtualKeyUpdated event")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_CacheInvalidationFails_StillUpdatesKey()
        {
            // Arrange
            int keyId = 4;
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                MaxBudget = 500
            };

            var existingKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Test Key",
                KeyHash = "key-hash-abc",
                MaxBudget = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingKey);

            _mockVirtualKeyRepository
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Setup cache invalidation to throw exception
            _mockCache
                .Setup(c => c.InvalidateVirtualKeyAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);

            // Assert - Update should still succeed
            Assert.True(result);

            // Verify warning was logged about cache failure
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

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
                .ReturnsAsync((VirtualKey?)null);

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

            // Setup GetByIdAsync to return a virtual key
            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VirtualKey { Id = keyId, KeyName = "Test Key" });

            // Setup DeleteAsync to return true
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
        public async Task DeleteVirtualKeyAsync_ExistingId_PublishesVirtualKeyDeletedEvent()
        {
            // Arrange
            int keyId = 1;
            var virtualKey = new VirtualKey 
            { 
                Id = keyId, 
                KeyName = "Test Key",
                KeyHash = "test-hash-123"
            };

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            _mockVirtualKeyRepository
                .Setup(r => r.DeleteAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteVirtualKeyAsync(keyId);

            // Assert
            Assert.True(result);

            // Verify event was published
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<VirtualKeyDeleted>(e => 
                    e.KeyId == keyId &&
                    e.KeyHash == "test-hash-123" &&
                    e.KeyName == "Test Key"),
                It.IsAny<CancellationToken>()),
                Times.Once);
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
                .ReturnsAsync((VirtualKey?)null);

            // Act
            var result = await _service.ResetSpendAsync(keyId);

            // Assert
            Assert.False(result);

            // Verify repository methods were not called
            _mockSpendHistoryRepository.Verify(r => r.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockVirtualKeyRepository.Verify(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #region Event Partition Key Tests

        [Fact]
        public async Task VirtualKeyEvents_UseKeyIdAsPartitionKey()
        {
            // This test verifies that events use the correct partition key for ordered processing
            // Arrange
            var request = new CreateVirtualKeyRequestDto
            {
                KeyName = "Partition Test Key"
            };

            var createdKey = new VirtualKey
            {
                Id = 999,
                KeyName = "Partition Test Key",
                KeyHash = "partition-test-hash",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(999);

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdKey);

            VirtualKeyUpdated? capturedEvent = null;
            _mockPublishEndpoint
                .Setup(p => p.Publish(It.IsAny<VirtualKeyUpdated>(), It.IsAny<CancellationToken>()))
                .Callback<object, CancellationToken>((evt, ct) => capturedEvent = evt as VirtualKeyUpdated)
                .Returns(Task.CompletedTask);

            // Act
            await _service.GenerateVirtualKeyAsync(request);

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.Equal("999", capturedEvent.PartitionKey);
            Assert.Equal(999, capturedEvent.KeyId);
        }

        #endregion

        #region Integration Scenarios

        [Fact]
        public async Task FullLifecycle_CreateUpdateDelete_PublishesAllEvents()
        {
            // This test simulates a full virtual key lifecycle
            // Arrange
            int keyId = 100;
            var createRequest = new CreateVirtualKeyRequestDto
            {
                KeyName = "Lifecycle Test Key",
                MaxBudget = 100
            };

            var createdKey = new VirtualKey
            {
                Id = keyId,
                KeyName = "Lifecycle Test Key",
                KeyHash = "lifecycle-hash",
                MaxBudget = 100,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Setup for create
            _mockVirtualKeyRepository
                .Setup(r => r.CreateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(keyId);

            _mockVirtualKeyRepository
                .Setup(r => r.GetByIdAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdKey);

            // Act 1: Create
            var createResult = await _service.GenerateVirtualKeyAsync(createRequest);
            Assert.NotNull(createResult);

            // Verify create event
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<VirtualKeyUpdated>(e => 
                    e.KeyId == keyId &&
                    e.ChangedProperties.Contains("Created")),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Setup for update
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                IsEnabled = false
            };

            _mockVirtualKeyRepository
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act 2: Update
            var updateResult = await _service.UpdateVirtualKeyAsync(keyId, updateRequest);
            Assert.True(updateResult);

            // Verify update event
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<VirtualKeyUpdated>(e => 
                    e.KeyId == keyId &&
                    e.ChangedProperties.Contains("IsEnabled")),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Setup for delete
            _mockVirtualKeyRepository
                .Setup(r => r.DeleteAsync(keyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act 3: Delete
            var deleteResult = await _service.DeleteVirtualKeyAsync(keyId);
            Assert.True(deleteResult);

            // Verify delete event
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.Is<VirtualKeyDeleted>(e => 
                    e.KeyId == keyId &&
                    e.KeyHash == "lifecycle-hash"),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Total: 2 VirtualKeyUpdated events
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.IsAny<VirtualKeyUpdated>(),
                It.IsAny<CancellationToken>()),
                Times.Exactly(2));
                
            // Total: 1 VirtualKeyDeleted event
            _mockPublishEndpoint.Verify(p => p.Publish(
                It.IsAny<VirtualKeyDeleted>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion
    }
}
