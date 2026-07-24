using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class MediaLifecycleServiceTests
    {
        #region CleanupExpiredMediaAsync Tests

        [Fact]
        public async Task CleanupExpiredMediaAsync_WithAutoCleanupEnabled_ShouldDeleteExpiredMedia()
        {
            // Arrange
            var expiredMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/expired1.jpg", 
                    ExpiresAt = DateTime.UtcNow.AddDays(-1) 
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/expired2.jpg", 
                    ExpiresAt = DateTime.UtcNow.AddDays(-2) 
                }
            };

            _mockMediaRepository.Setup(x => x.GetExpiredMediaAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(expiredMedia);

            _mockStorageService.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CleanupExpiredMediaAsync();

            // Assert
            Assert.Equal(2, result.DeletedCount);
            Assert.Equal(0, result.FailedCount);

            foreach (var media in expiredMedia)
            {
                _mockStorageService.Verify(x => x.DeleteAsync(media.StorageKey), Times.Once);
                _mockMediaRepository.Verify(x => x.DeleteAsync(media.Id), Times.Once);
            }
        }

        [Fact]
        public async Task CleanupExpiredMediaAsync_WithAutoCleanupDisabled_ShouldReturnZero()
        {
            // Arrange
            _options.EnableAutoCleanup = false;

            // Act
            var result = await _service.CleanupExpiredMediaAsync();

            // Assert
            Assert.Equal(MediaDeletionResult.Empty, result);
            _mockMediaRepository.Verify(x => x.GetExpiredMediaAsync(It.IsAny<DateTime>()), Times.Never);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupExpiredMediaAsync_WithStorageDeleteFailure_PreservesFailedRecord(
            bool throws)
        {
            // Arrange
            var expiredMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/expired1.jpg", 
                    ExpiresAt = DateTime.UtcNow.AddDays(-1) 
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/expired2.jpg", 
                    ExpiresAt = DateTime.UtcNow.AddDays(-2) 
                }
            };

            _mockMediaRepository.Setup(x => x.GetExpiredMediaAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(expiredMedia);

            var failedDelete = _mockStorageService.Setup(x => x.DeleteAsync("image/expired1.jpg"));
            if (throws)
            {
                failedDelete.ThrowsAsync(new Exception("Storage delete failed"));
            }
            else
            {
                failedDelete.ReturnsAsync(false);
            }
            _mockStorageService.Setup(x => x.DeleteAsync("image/expired2.jpg"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CleanupExpiredMediaAsync();

            // Assert
            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);

            _mockStorageService.Verify(x => x.DeleteAsync("image/expired1.jpg"), Times.Once);
            _mockStorageService.Verify(x => x.DeleteAsync("image/expired2.jpg"), Times.Once);
            _mockMediaRepository.Verify(x => x.DeleteAsync(expiredMedia[1].Id), Times.Once);
            _mockMediaRepository.Verify(x => x.DeleteAsync(expiredMedia[0].Id), Times.Never);
        }

        #endregion

        #region CleanupOrphanedMediaAsync Tests

        [Fact]
        public async Task CleanupOrphanedMediaAsync_WithOrphanCleanupEnabled_ShouldDeleteOrphanedMedia()
        {
            // Arrange
            var orphanedMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/orphaned1.jpg", 
                    VirtualKeyId = 999 
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/orphaned2.jpg", 
                    VirtualKeyId = 998 
                }
            };

            _mockMediaRepository.Setup(x => x.GetOrphanedMediaAsync())
                .ReturnsAsync(orphanedMedia);

            _mockStorageService.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CleanupOrphanedMediaAsync();

            // Assert
            Assert.Equal(2, result.DeletedCount);
            Assert.Equal(0, result.FailedCount);

            foreach (var media in orphanedMedia)
            {
                _mockStorageService.Verify(x => x.DeleteAsync(media.StorageKey), Times.Once);
                _mockMediaRepository.Verify(x => x.DeleteAsync(media.Id), Times.Once);
            }
        }

        [Fact]
        public async Task CleanupOrphanedMediaAsync_WithOrphanCleanupDisabled_ShouldReturnZero()
        {
            // Arrange
            _options.OrphanCleanupEnabled = false;

            // Act
            var result = await _service.CleanupOrphanedMediaAsync();

            // Assert
            Assert.Equal(MediaDeletionResult.Empty, result);
            _mockMediaRepository.Verify(x => x.GetOrphanedMediaAsync(), Times.Never);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupOrphanedMediaAsync_WithStorageDeleteFailure_PreservesRecord(
            bool throws)
        {
            var media = new MediaRecord
            {
                Id = Guid.NewGuid(),
                StorageKey = "image/orphaned-failure.jpg",
                VirtualKeyId = 999
            };
            _mockMediaRepository.Setup(x => x.GetOrphanedMediaAsync())
                .ReturnsAsync(new List<MediaRecord> { media });
            var failedDelete = _mockStorageService.Setup(x => x.DeleteAsync(media.StorageKey));
            if (throws)
            {
                failedDelete.ThrowsAsync(new Exception("Storage delete failed"));
            }
            else
            {
                failedDelete.ReturnsAsync(false);
            }

            var result = await _service.CleanupOrphanedMediaAsync();

            Assert.Equal(0, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);
            _mockMediaRepository.Verify(x => x.DeleteAsync(media.Id), Times.Never);
        }

        #endregion

        #region PruneOldMediaAsync Tests

        [Fact]
        public async Task PruneOldMediaAsync_WithValidDaysToKeep_ShouldPruneOldMedia()
        {
            // Arrange
            var daysToKeep = 30;
            var oldMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/old1.jpg", 
                    CreatedAt = DateTime.UtcNow.AddDays(-40),
                    LastAccessedAt = null
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/old2.jpg", 
                    CreatedAt = DateTime.UtcNow.AddDays(-50),
                    LastAccessedAt = DateTime.UtcNow.AddDays(-45)
                }
            };

            _mockMediaRepository.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(oldMedia);

            _mockStorageService.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.PruneOldMediaAsync(daysToKeep);

            // Assert
            Assert.Equal(2, result.DeletedCount);
            Assert.Equal(0, result.FailedCount);

            foreach (var media in oldMedia)
            {
                _mockStorageService.Verify(x => x.DeleteAsync(media.StorageKey), Times.Once);
                _mockMediaRepository.Verify(x => x.DeleteAsync(media.Id), Times.Once);
            }
        }

        [Fact]
        public async Task PruneOldMediaAsync_WithRecentlyAccessedMedia_ShouldSkipRecentlyAccessed()
        {
            // Arrange
            var daysToKeep = 30;
            var oldMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/old1.jpg", 
                    CreatedAt = DateTime.UtcNow.AddDays(-40),
                    LastAccessedAt = DateTime.UtcNow.AddDays(-20) // Recently accessed
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "image/old2.jpg", 
                    CreatedAt = DateTime.UtcNow.AddDays(-50),
                    LastAccessedAt = DateTime.UtcNow.AddDays(-45) // Not recently accessed
                }
            };

            _mockMediaRepository.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(oldMedia);

            _mockStorageService.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.PruneOldMediaAsync(daysToKeep, respectRecentAccess: true);

            // Assert
            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(0, result.FailedCount);

            _mockStorageService.Verify(x => x.DeleteAsync("image/old1.jpg"), Times.Never);
            _mockStorageService.Verify(x => x.DeleteAsync("image/old2.jpg"), Times.Once);
            _mockMediaRepository.Verify(x => x.DeleteAsync(oldMedia[0].Id), Times.Never);
            _mockMediaRepository.Verify(x => x.DeleteAsync(oldMedia[1].Id), Times.Once);
        }

        [Fact]
        public async Task PruneOldMediaAsync_WithAutoCleanupDisabled_ShouldReturnZero()
        {
            // Arrange
            _options.EnableAutoCleanup = false;
            var daysToKeep = 30;

            // Act
            var result = await _service.PruneOldMediaAsync(daysToKeep);

            // Assert
            Assert.Equal(MediaDeletionResult.Empty, result);
            _mockMediaRepository.Verify(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()), Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task PruneOldMediaAsync_WithInvalidDaysToKeep_ShouldReturnZero(int invalidDaysToKeep)
        {
            // Act
            var result = await _service.PruneOldMediaAsync(invalidDaysToKeep);

            // Assert
            Assert.Equal(MediaDeletionResult.Empty, result);
            _mockMediaRepository.Verify(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PruneOldMediaAsync_WithStorageDeleteFailure_PreservesRecord(
            bool throws)
        {
            var media = new MediaRecord
            {
                Id = Guid.NewGuid(),
                StorageKey = "image/prune-failure.jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };
            _mockMediaRepository.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<MediaRecord> { media });
            var failedDelete = _mockStorageService.Setup(x => x.DeleteAsync(media.StorageKey));
            if (throws)
            {
                failedDelete.ThrowsAsync(new Exception("Storage delete failed"));
            }
            else
            {
                failedDelete.ReturnsAsync(false);
            }

            var result = await _service.PruneOldMediaAsync(30);

            Assert.Equal(0, result.DeletedCount);
            Assert.Equal(1, result.FailedCount);
            _mockMediaRepository.Verify(x => x.DeleteAsync(media.Id), Times.Never);
        }

        #endregion
    }
}
