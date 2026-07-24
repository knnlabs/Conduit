using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class MediaLifecycleServiceTests
    {
        #region DeleteMediaForVirtualKeyAsync Tests

        [Fact]
        public async Task DeleteMediaForVirtualKeyAsync_WithAutoCleanupEnabled_ShouldDeleteAllMedia()
        {
            // Arrange
            var virtualKeyId = 1;
            var mediaRecords = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "image/1.jpg", VirtualKeyId = virtualKeyId },
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "image/2.jpg", VirtualKeyId = virtualKeyId },
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "video/1.mp4", VirtualKeyId = virtualKeyId }
            };

            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(mediaRecords);

            _mockStorageService.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMediaForVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(3, result.DeletedCount);
            Assert.Equal(0, result.FailedCount);

            foreach (var media in mediaRecords)
            {
                _mockStorageService.Verify(x => x.DeleteAsync(media.StorageKey), Times.Once);
                _mockMediaRepository.Verify(x => x.DeleteAsync(media.Id), Times.Once);
            }
        }

        [Fact]
        public async Task DeleteMediaForVirtualKeyAsync_WithAutoCleanupDisabled_ShouldReturnZero()
        {
            // Arrange
            var virtualKeyId = 1;
            _options.EnableAutoCleanup = false;

            // Act
            var result = await _service.DeleteMediaForVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(MediaDeletionResult.Empty, result);
            _mockMediaRepository.Verify(x => x.GetByVirtualKeyIdAsync(It.IsAny<int>()), Times.Never);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DeleteMediaForVirtualKeyAsync_WithStorageDeleteFailure_PreservesFailedRecord(
            bool throws)
        {
            // Arrange
            var virtualKeyId = 1;
            var mediaRecords = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "image/1.jpg", VirtualKeyId = virtualKeyId },
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "image/2.jpg", VirtualKeyId = virtualKeyId }
            };

            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(mediaRecords);

            var failedDelete = _mockStorageService.Setup(x => x.DeleteAsync("image/1.jpg"));
            if (throws)
            {
                failedDelete.ThrowsAsync(new Exception("Storage delete failed"));
            }
            else
            {
                failedDelete.ReturnsAsync(false);
            }
            _mockStorageService.Setup(x => x.DeleteAsync("image/2.jpg"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMediaForVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);

            _mockStorageService.Verify(x => x.DeleteAsync("image/1.jpg"), Times.Once);
            _mockStorageService.Verify(x => x.DeleteAsync("image/2.jpg"), Times.Once);
            _mockMediaRepository.Verify(x => x.DeleteAsync(mediaRecords[1].Id), Times.Once);
            _mockMediaRepository.Verify(x => x.DeleteAsync(mediaRecords[0].Id), Times.Never);
        }

        [Fact]
        public async Task DeleteMediaForVirtualKeyAsync_WithNoMediaRecords_ShouldReturnZero()
        {
            // Arrange
            var virtualKeyId = 1;
            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(new List<MediaRecord>());

            // Act
            var result = await _service.DeleteMediaForVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(MediaDeletionResult.Empty, result);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
            _mockMediaRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        #endregion
    }
}
