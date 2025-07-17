using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class MediaLifecycleServiceTests
    {
        private readonly Mock<IMediaRecordRepository> _mockMediaRepository;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<ILogger<MediaLifecycleService>> _mockLogger;
        private readonly Mock<IOptions<MediaManagementOptions>> _mockOptions;
        private readonly MediaManagementOptions _options;
        private readonly MediaLifecycleService _service;

        public MediaLifecycleServiceTests()
        {
            _mockMediaRepository = new Mock<IMediaRecordRepository>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockLogger = new Mock<ILogger<MediaLifecycleService>>();
            _mockOptions = new Mock<IOptions<MediaManagementOptions>>();

            _options = new MediaManagementOptions
            {
                EnableOwnershipTracking = true,
                EnableAutoCleanup = true,
                MediaRetentionDays = 90,
                OrphanCleanupEnabled = true,
                AccessControlEnabled = false
            };

            _mockOptions.Setup(x => x.Value).Returns(_options);

            _service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                _mockOptions.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullMediaRepository_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MediaLifecycleService(
                null,
                _mockStorageService.Object,
                _mockLogger.Object,
                _mockOptions.Object));
        }

        [Fact]
        public void Constructor_WithNullStorageService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MediaLifecycleService(
                _mockMediaRepository.Object,
                null,
                _mockLogger.Object,
                _mockOptions.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                null,
                _mockOptions.Object));
        }

        [Fact]
        public void Constructor_WithNullOptions_ShouldUseDefaultOptions()
        {
            // Act
            var service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                null);

            // Assert - Should not throw and use default configuration
            Assert.NotNull(service);
        }

        #endregion

        #region TrackMediaAsync Tests

        [Fact]
        public async Task TrackMediaAsync_WithValidParameters_ShouldCreateMediaRecord()
        {
            // Arrange
            var virtualKeyId = 1;
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var mediaType = "image";
            var metadata = new MediaLifecycleMetadata
            {
                ContentType = "image/jpeg",
                SizeBytes = 1024,
                ContentHash = "test-hash",
                Provider = "openai",
                Model = "dall-e-3",
                Prompt = "A beautiful landscape",
                StorageUrl = "https://storage.example.com/image.jpg",
                PublicUrl = "https://cdn.example.com/image.jpg",
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            var expectedMediaRecord = new MediaRecord
            {
                Id = Guid.NewGuid(),
                StorageKey = storageKey,
                VirtualKeyId = virtualKeyId,
                MediaType = mediaType,
                ContentType = metadata.ContentType,
                SizeBytes = metadata.SizeBytes,
                ContentHash = metadata.ContentHash,
                Provider = metadata.Provider,
                Model = metadata.Model,
                Prompt = metadata.Prompt,
                StorageUrl = metadata.StorageUrl,
                PublicUrl = metadata.PublicUrl,
                ExpiresAt = metadata.ExpiresAt,
                CreatedAt = DateTime.UtcNow,
                AccessCount = 0
            };

            _mockMediaRepository.Setup(x => x.CreateAsync(It.IsAny<MediaRecord>()))
                .ReturnsAsync(expectedMediaRecord);

            // Act
            var result = await _service.TrackMediaAsync(virtualKeyId, storageKey, mediaType, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(storageKey, result.StorageKey);
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(mediaType, result.MediaType);
            Assert.Equal(metadata.ContentType, result.ContentType);
            Assert.Equal(metadata.SizeBytes, result.SizeBytes);
            Assert.Equal(metadata.ContentHash, result.ContentHash);
            Assert.Equal(metadata.Provider, result.Provider);
            Assert.Equal(metadata.Model, result.Model);
            Assert.Equal(metadata.Prompt, result.Prompt);
            Assert.Equal(metadata.StorageUrl, result.StorageUrl);
            Assert.Equal(metadata.PublicUrl, result.PublicUrl);
            Assert.Equal(metadata.ExpiresAt, result.ExpiresAt);
            Assert.Equal(0, result.AccessCount);

            _mockMediaRepository.Verify(x => x.CreateAsync(It.Is<MediaRecord>(r =>
                r.StorageKey == storageKey &&
                r.VirtualKeyId == virtualKeyId &&
                r.MediaType == mediaType &&
                r.ContentType == metadata.ContentType &&
                r.SizeBytes == metadata.SizeBytes &&
                r.ContentHash == metadata.ContentHash &&
                r.Provider == metadata.Provider &&
                r.Model == metadata.Model &&
                r.Prompt == metadata.Prompt &&
                r.StorageUrl == metadata.StorageUrl &&
                r.PublicUrl == metadata.PublicUrl &&
                r.ExpiresAt == metadata.ExpiresAt &&
                r.AccessCount == 0
            )), Times.Once);
        }

        [Fact]
        public async Task TrackMediaAsync_WithNullMetadata_ShouldCreateMediaRecordWithNullValues()
        {
            // Arrange
            var virtualKeyId = 1;
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var mediaType = "image";

            var expectedMediaRecord = new MediaRecord
            {
                Id = Guid.NewGuid(),
                StorageKey = storageKey,
                VirtualKeyId = virtualKeyId,
                MediaType = mediaType,
                ContentType = null,
                SizeBytes = null,
                ContentHash = null,
                Provider = null,
                Model = null,
                Prompt = null,
                StorageUrl = null,
                PublicUrl = null,
                ExpiresAt = null,
                CreatedAt = DateTime.UtcNow,
                AccessCount = 0
            };

            _mockMediaRepository.Setup(x => x.CreateAsync(It.IsAny<MediaRecord>()))
                .ReturnsAsync(expectedMediaRecord);

            // Act
            var result = await _service.TrackMediaAsync(virtualKeyId, storageKey, mediaType, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(storageKey, result.StorageKey);
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(mediaType, result.MediaType);
            Assert.Null(result.ContentType);
            Assert.Null(result.SizeBytes);
            Assert.Null(result.ContentHash);
            Assert.Null(result.Provider);
            Assert.Null(result.Model);
            Assert.Null(result.Prompt);
            Assert.Null(result.StorageUrl);
            Assert.Null(result.PublicUrl);
            Assert.Null(result.ExpiresAt);
            Assert.Equal(0, result.AccessCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task TrackMediaAsync_WithInvalidVirtualKeyId_ShouldThrowArgumentException(int invalidVirtualKeyId)
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var mediaType = "image";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.TrackMediaAsync(invalidVirtualKeyId, storageKey, mediaType, null));

            Assert.Equal("virtualKeyId", exception.ParamName);
            Assert.Contains("Virtual key ID must be positive", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task TrackMediaAsync_WithInvalidStorageKey_ShouldThrowArgumentException(string invalidStorageKey)
        {
            // Arrange
            var virtualKeyId = 1;
            var mediaType = "image";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.TrackMediaAsync(virtualKeyId, invalidStorageKey, mediaType, null));

            Assert.Equal("storageKey", exception.ParamName);
            Assert.Contains("Storage key cannot be empty", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task TrackMediaAsync_WithInvalidMediaType_ShouldThrowArgumentException(string invalidMediaType)
        {
            // Arrange
            var virtualKeyId = 1;
            var storageKey = "image/2023/01/01/test-hash.jpg";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.TrackMediaAsync(virtualKeyId, storageKey, invalidMediaType, null));

            Assert.Equal("mediaType", exception.ParamName);
            Assert.Contains("Media type cannot be empty", exception.Message);
        }

        #endregion

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
            Assert.Equal(3, result);

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
            Assert.Equal(0, result);
            _mockMediaRepository.Verify(x => x.GetByVirtualKeyIdAsync(It.IsAny<int>()), Times.Never);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteMediaForVirtualKeyAsync_WithStorageDeleteFailure_ShouldContinueWithOtherMedia()
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

            // First storage delete fails, second succeeds
            _mockStorageService.Setup(x => x.DeleteAsync("image/1.jpg"))
                .ThrowsAsync(new Exception("Storage delete failed"));
            _mockStorageService.Setup(x => x.DeleteAsync("image/2.jpg"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMediaForVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(1, result); // Only one successful deletion

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
            Assert.Equal(0, result);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
            _mockMediaRepository.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        #endregion

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
            Assert.Equal(2, result);

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
            Assert.Equal(0, result);
            _mockMediaRepository.Verify(x => x.GetExpiredMediaAsync(It.IsAny<DateTime>()), Times.Never);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CleanupExpiredMediaAsync_WithStorageDeleteFailure_ShouldContinueWithOtherMedia()
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

            // First storage delete fails, second succeeds
            _mockStorageService.Setup(x => x.DeleteAsync("image/expired1.jpg"))
                .ThrowsAsync(new Exception("Storage delete failed"));
            _mockStorageService.Setup(x => x.DeleteAsync("image/expired2.jpg"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CleanupExpiredMediaAsync();

            // Assert
            Assert.Equal(1, result); // Only one successful deletion

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
            Assert.Equal(2, result);

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
            Assert.Equal(0, result);
            _mockMediaRepository.Verify(x => x.GetOrphanedMediaAsync(), Times.Never);
            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
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
            Assert.Equal(2, result);

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
            Assert.Equal(1, result); // Only one should be deleted

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
            Assert.Equal(0, result);
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
            Assert.Equal(0, result);
            _mockMediaRepository.Verify(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()), Times.Never);
        }

        #endregion

        #region UpdateAccessStatsAsync Tests

        [Fact]
        public async Task UpdateAccessStatsAsync_WithValidStorageKey_ShouldUpdateAccessStats()
        {
            // Arrange
            var storageKey = "image/test.jpg";
            var mediaRecord = new MediaRecord
            {
                Id = Guid.NewGuid(),
                StorageKey = storageKey,
                AccessCount = 5
            };

            _mockMediaRepository.Setup(x => x.GetByStorageKeyAsync(storageKey))
                .ReturnsAsync(mediaRecord);

            _mockMediaRepository.Setup(x => x.UpdateAccessStatsAsync(mediaRecord.Id))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateAccessStatsAsync(storageKey);

            // Assert
            Assert.True(result);
            _mockMediaRepository.Verify(x => x.GetByStorageKeyAsync(storageKey), Times.Once);
            _mockMediaRepository.Verify(x => x.UpdateAccessStatsAsync(mediaRecord.Id), Times.Once);
        }

        [Fact]
        public async Task UpdateAccessStatsAsync_WithNonExistentStorageKey_ShouldReturnFalse()
        {
            // Arrange
            var storageKey = "image/non-existent.jpg";

            _mockMediaRepository.Setup(x => x.GetByStorageKeyAsync(storageKey))
                .ReturnsAsync((MediaRecord)null);

            // Act
            var result = await _service.UpdateAccessStatsAsync(storageKey);

            // Assert
            Assert.False(result);
            _mockMediaRepository.Verify(x => x.GetByStorageKeyAsync(storageKey), Times.Once);
            _mockMediaRepository.Verify(x => x.UpdateAccessStatsAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateAccessStatsAsync_WithInvalidStorageKey_ShouldReturnFalse(string invalidStorageKey)
        {
            // Act
            var result = await _service.UpdateAccessStatsAsync(invalidStorageKey);

            // Assert
            Assert.False(result);
            _mockMediaRepository.Verify(x => x.GetByStorageKeyAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAccessStatsAsync_WithRepositoryException_ShouldReturnFalse()
        {
            // Arrange
            var storageKey = "image/test.jpg";

            _mockMediaRepository.Setup(x => x.GetByStorageKeyAsync(storageKey))
                .ThrowsAsync(new Exception("Repository error"));

            // Act
            var result = await _service.UpdateAccessStatsAsync(storageKey);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GetStorageStatsByVirtualKeyAsync Tests

        [Fact]
        public async Task GetStorageStatsByVirtualKeyAsync_WithValidVirtualKey_ShouldReturnStats()
        {
            // Arrange
            var virtualKeyId = 1;
            var mediaRecords = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = virtualKeyId, 
                    MediaType = "image", 
                    SizeBytes = 1024 
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = virtualKeyId, 
                    MediaType = "image", 
                    SizeBytes = 2048 
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = virtualKeyId, 
                    MediaType = "video", 
                    SizeBytes = 5000000 
                }
            };

            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(mediaRecords);

            // Act
            var result = await _service.GetStorageStatsByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(3, result.TotalFiles);
            Assert.Equal(5003072, result.TotalSizeBytes); // 1024 + 2048 + 5000000

            Assert.Equal(2, result.ByMediaType["image"].FileCount);
            Assert.Equal(3072, result.ByMediaType["image"].SizeBytes); // 1024 + 2048

            Assert.Equal(1, result.ByMediaType["video"].FileCount);
            Assert.Equal(5000000, result.ByMediaType["video"].SizeBytes);
        }

        [Fact]
        public async Task GetStorageStatsByVirtualKeyAsync_WithNoMediaRecords_ShouldReturnEmptyStats()
        {
            // Arrange
            var virtualKeyId = 1;

            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(new List<MediaRecord>());

            // Act
            var result = await _service.GetStorageStatsByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(0, result.TotalFiles);
            Assert.Equal(0, result.TotalSizeBytes);
            Assert.Empty(result.ByMediaType);
        }

        [Fact]
        public async Task GetStorageStatsByVirtualKeyAsync_WithNullSizeBytes_ShouldTreatAsZero()
        {
            // Arrange
            var virtualKeyId = 1;
            var mediaRecords = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = virtualKeyId, 
                    MediaType = "image", 
                    SizeBytes = null 
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    VirtualKeyId = virtualKeyId, 
                    MediaType = "image", 
                    SizeBytes = 1024 
                }
            };

            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(mediaRecords);

            // Act
            var result = await _service.GetStorageStatsByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalFiles);
            Assert.Equal(1024, result.TotalSizeBytes); // null treated as 0
            Assert.Equal(2, result.ByMediaType["image"].FileCount);
            Assert.Equal(1024, result.ByMediaType["image"].SizeBytes);
        }

        #endregion

        #region GetOverallStorageStatsAsync Tests

        [Fact]
        public async Task GetOverallStorageStatsAsync_ShouldReturnOverallStats()
        {
            // Arrange
            var byProvider = new Dictionary<string, long>
            {
                ["openai"] = 1000000,
                ["minimax"] = 2000000
            };

            var byMediaType = new Dictionary<string, long>
            {
                ["image"] = 500000,
                ["video"] = 2500000
            };

            var allMedia = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid() },
                new MediaRecord { Id = Guid.NewGuid() },
                new MediaRecord { Id = Guid.NewGuid() }
            };

            var orphanedMedia = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid() }
            };

            _mockMediaRepository.Setup(x => x.GetStorageStatsByProviderAsync())
                .ReturnsAsync(byProvider);

            _mockMediaRepository.Setup(x => x.GetStorageStatsByMediaTypeAsync())
                .ReturnsAsync(byMediaType);

            _mockMediaRepository.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(allMedia);

            _mockMediaRepository.Setup(x => x.GetOrphanedMediaAsync())
                .ReturnsAsync(orphanedMedia);

            // Act
            var result = await _service.GetOverallStorageStatsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3000000, result.TotalSizeBytes); // Sum of byMediaType values
            Assert.Equal(3, result.TotalFiles);
            Assert.Equal(1, result.OrphanedFiles);
            Assert.Equal(byProvider, result.ByProvider);
            Assert.Equal(byMediaType, result.ByMediaType);
        }

        #endregion

        #region GetMediaByVirtualKeyAsync Tests

        [Fact]
        public async Task GetMediaByVirtualKeyAsync_WithValidVirtualKey_ShouldReturnMediaRecords()
        {
            // Arrange
            var virtualKeyId = 1;
            var mediaRecords = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid(), VirtualKeyId = virtualKeyId },
                new MediaRecord { Id = Guid.NewGuid(), VirtualKeyId = virtualKeyId }
            };

            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(mediaRecords);

            // Act
            var result = await _service.GetMediaByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(mediaRecords, result);
        }

        [Fact]
        public async Task GetMediaByVirtualKeyAsync_WithRepositoryException_ShouldThrowException()
        {
            // Arrange
            var virtualKeyId = 1;

            _mockMediaRepository.Setup(x => x.GetByVirtualKeyIdAsync(virtualKeyId))
                .ThrowsAsync(new Exception("Repository error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.GetMediaByVirtualKeyAsync(virtualKeyId));
        }

        #endregion
    }
}