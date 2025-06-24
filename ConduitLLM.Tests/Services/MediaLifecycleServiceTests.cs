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

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for MediaLifecycleService.
    /// </summary>
    public class MediaLifecycleServiceTests
    {
        private readonly Mock<IMediaRecordRepository> _mockMediaRepository;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<ILogger<MediaLifecycleService>> _mockLogger;
        private readonly IOptions<MediaManagementOptions> _options;
        private readonly MediaLifecycleService _service;

        public MediaLifecycleServiceTests()
        {
            _mockMediaRepository = new Mock<IMediaRecordRepository>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockLogger = new Mock<ILogger<MediaLifecycleService>>();
            _options = Options.Create(new MediaManagementOptions
            {
                EnableOwnershipTracking = true,
                EnableAutoCleanup = true,
                MediaRetentionDays = 90,
                OrphanCleanupEnabled = true,
                AccessControlEnabled = false
            });

            _service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                _options);
        }

        [Fact]
        public async Task TrackMediaAsync_ValidInput_CreatesMediaRecord()
        {
            // Arrange
            var virtualKeyId = 123;
            var storageKey = "test-storage-key";
            var mediaType = "image";
            var metadata = new MediaLifecycleMetadata
            {
                ContentType = "image/png",
                SizeBytes = 1024,
                Provider = "openai",
                Model = "dall-e-3",
                Prompt = "test prompt"
            };

            _mockMediaRepository
                .Setup(r => r.CreateAsync(It.IsAny<MediaRecord>()))
                .ReturnsAsync((MediaRecord record) => record);

            // Act
            var result = await _service.TrackMediaAsync(virtualKeyId, storageKey, mediaType, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(storageKey, result.StorageKey);
            Assert.Equal(mediaType, result.MediaType);
            Assert.Equal(metadata.ContentType, result.ContentType);
            Assert.Equal(metadata.SizeBytes, result.SizeBytes);
            Assert.Equal(metadata.Provider, result.Provider);
            Assert.Equal(metadata.Model, result.Model);
            Assert.Equal(metadata.Prompt, result.Prompt);

            _mockMediaRepository.Verify(r => r.CreateAsync(It.IsAny<MediaRecord>()), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task TrackMediaAsync_InvalidVirtualKeyId_ThrowsArgumentException(int invalidId)
        {
            // Arrange
            var metadata = new MediaLifecycleMetadata();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.TrackMediaAsync(invalidId, "key", "image", metadata));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task TrackMediaAsync_InvalidStorageKey_ThrowsArgumentException(string invalidKey)
        {
            // Arrange
            var metadata = new MediaLifecycleMetadata();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.TrackMediaAsync(1, invalidKey, "image", metadata));
        }

        [Fact]
        public async Task DeleteMediaForVirtualKeyAsync_WithAutoCleanupEnabled_DeletesMedia()
        {
            // Arrange
            var virtualKeyId = 123;
            var mediaRecords = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "key1", VirtualKeyId = virtualKeyId },
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "key2", VirtualKeyId = virtualKeyId }
            };

            _mockMediaRepository
                .Setup(r => r.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(mediaRecords);

            _mockStorageService
                .Setup(s => s.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMediaRepository
                .Setup(r => r.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMediaForVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(2, result);
            _mockStorageService.Verify(s => s.DeleteAsync("key1"), Times.Once);
            _mockStorageService.Verify(s => s.DeleteAsync("key2"), Times.Once);
            _mockMediaRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Exactly(2));
        }

        [Fact]
        public async Task DeleteMediaForVirtualKeyAsync_WithAutoCleanupDisabled_ReturnsZero()
        {
            // Arrange
            var options = Options.Create(new MediaManagementOptions { EnableAutoCleanup = false });
            var service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                options);

            // Act
            var result = await service.DeleteMediaForVirtualKeyAsync(123);

            // Assert
            Assert.Equal(0, result);
            _mockMediaRepository.Verify(r => r.GetByVirtualKeyIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task CleanupExpiredMediaAsync_WithExpiredMedia_DeletesExpiredFiles()
        {
            // Arrange
            var expiredMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "expired1", 
                    ExpiresAt = DateTime.UtcNow.AddDays(-1) 
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "expired2", 
                    ExpiresAt = DateTime.UtcNow.AddDays(-2) 
                }
            };

            _mockMediaRepository
                .Setup(r => r.GetExpiredMediaAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(expiredMedia);

            _mockStorageService
                .Setup(s => s.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMediaRepository
                .Setup(r => r.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CleanupExpiredMediaAsync();

            // Assert
            Assert.Equal(2, result);
            _mockStorageService.Verify(s => s.DeleteAsync("expired1"), Times.Once);
            _mockStorageService.Verify(s => s.DeleteAsync("expired2"), Times.Once);
        }

        [Fact]
        public async Task CleanupOrphanedMediaAsync_WithOrphanedMedia_DeletesOrphanedFiles()
        {
            // Arrange
            var orphanedMedia = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid(), StorageKey = "orphan1", VirtualKeyId = 999 }
            };

            _mockMediaRepository
                .Setup(r => r.GetOrphanedMediaAsync())
                .ReturnsAsync(orphanedMedia);

            _mockStorageService
                .Setup(s => s.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMediaRepository
                .Setup(r => r.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CleanupOrphanedMediaAsync();

            // Assert
            Assert.Equal(1, result);
            _mockStorageService.Verify(s => s.DeleteAsync("orphan1"), Times.Once);
        }

        [Fact]
        public async Task PruneOldMediaAsync_WithRecentAccess_SkipsRecentlyAccessedFiles()
        {
            // Arrange
            var oldMedia = new List<MediaRecord>
            {
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "old1", 
                    CreatedAt = DateTime.UtcNow.AddDays(-100),
                    LastAccessedAt = DateTime.UtcNow.AddDays(-5) // Recently accessed
                },
                new MediaRecord 
                { 
                    Id = Guid.NewGuid(), 
                    StorageKey = "old2", 
                    CreatedAt = DateTime.UtcNow.AddDays(-100),
                    LastAccessedAt = DateTime.UtcNow.AddDays(-60) // Not recently accessed
                }
            };

            _mockMediaRepository
                .Setup(r => r.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(oldMedia);

            _mockStorageService
                .Setup(s => s.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockMediaRepository
                .Setup(r => r.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.PruneOldMediaAsync(90, respectRecentAccess: true);

            // Assert
            Assert.Equal(1, result);
            _mockStorageService.Verify(s => s.DeleteAsync("old1"), Times.Never);
            _mockStorageService.Verify(s => s.DeleteAsync("old2"), Times.Once);
        }

        [Fact]
        public async Task UpdateAccessStatsAsync_ValidStorageKey_UpdatesStats()
        {
            // Arrange
            var storageKey = "test-key";
            var mediaRecord = new MediaRecord { Id = Guid.NewGuid(), StorageKey = storageKey };

            _mockMediaRepository
                .Setup(r => r.GetByStorageKeyAsync(storageKey))
                .ReturnsAsync(mediaRecord);

            _mockMediaRepository
                .Setup(r => r.UpdateAccessStatsAsync(mediaRecord.Id))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateAccessStatsAsync(storageKey);

            // Assert
            Assert.True(result);
            _mockMediaRepository.Verify(r => r.UpdateAccessStatsAsync(mediaRecord.Id), Times.Once);
        }

        [Fact]
        public async Task GetStorageStatsByVirtualKeyAsync_ReturnsCorrectStats()
        {
            // Arrange
            var virtualKeyId = 123;
            var mediaRecords = new List<MediaRecord>
            {
                new MediaRecord { MediaType = "image", SizeBytes = 1000 },
                new MediaRecord { MediaType = "image", SizeBytes = 2000 },
                new MediaRecord { MediaType = "video", SizeBytes = 5000 }
            };

            _mockMediaRepository
                .Setup(r => r.GetByVirtualKeyIdAsync(virtualKeyId))
                .ReturnsAsync(mediaRecords);

            // Act
            var result = await _service.GetStorageStatsByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(3, result.TotalFiles);
            Assert.Equal(8000, result.TotalSizeBytes);
            Assert.Equal(2, result.ByMediaType["image"].FileCount);
            Assert.Equal(3000, result.ByMediaType["image"].SizeBytes);
            Assert.Equal(1, result.ByMediaType["video"].FileCount);
            Assert.Equal(5000, result.ByMediaType["video"].SizeBytes);
        }

        [Fact]
        public async Task GetOverallStorageStatsAsync_ReturnsCorrectStats()
        {
            // Arrange
            var byProvider = new Dictionary<string, long>
            {
                { "openai", 10000 },
                { "minimax", 5000 }
            };

            var byMediaType = new Dictionary<string, long>
            {
                { "image", 12000 },
                { "video", 3000 }
            };

            var orphanedMedia = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid() },
                new MediaRecord { Id = Guid.NewGuid() }
            };

            _mockMediaRepository
                .Setup(r => r.GetStorageStatsByProviderAsync())
                .ReturnsAsync(byProvider);

            _mockMediaRepository
                .Setup(r => r.GetStorageStatsByMediaTypeAsync())
                .ReturnsAsync(byMediaType);

            _mockMediaRepository
                .Setup(r => r.GetOrphanedMediaAsync())
                .ReturnsAsync(orphanedMedia);

            _mockMediaRepository
                .Setup(r => r.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<MediaRecord> { new(), new(), new() });

            // Act
            var result = await _service.GetOverallStorageStatsAsync();

            // Assert
            Assert.Equal(15000, result.TotalSizeBytes);
            Assert.Equal(3, result.TotalFiles);
            Assert.Equal(2, result.OrphanedFiles);
            Assert.Equal(byProvider, result.ByProvider);
            Assert.Equal(byMediaType, result.ByMediaType);
        }
    }
}