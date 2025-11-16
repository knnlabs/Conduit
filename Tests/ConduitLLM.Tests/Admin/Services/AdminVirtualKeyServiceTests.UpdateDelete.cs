using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Events;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
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
    }
}