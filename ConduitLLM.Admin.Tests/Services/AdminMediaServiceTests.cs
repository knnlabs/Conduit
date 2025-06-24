using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    /// <summary>
    /// Unit tests for AdminMediaService.
    /// </summary>
    public class AdminMediaServiceTests
    {
        private readonly Mock<IMediaRecordRepository> _mockMediaRepository;
        private readonly Mock<IMediaLifecycleService> _mockMediaLifecycleService;
        private readonly Mock<ILogger<AdminMediaService>> _mockLogger;
        private readonly AdminMediaService _service;

        public AdminMediaServiceTests()
        {
            _mockMediaRepository = new Mock<IMediaRecordRepository>();
            _mockMediaLifecycleService = new Mock<IMediaLifecycleService>();
            _mockLogger = new Mock<ILogger<AdminMediaService>>();

            _service = new AdminMediaService(
                _mockMediaRepository.Object,
                _mockMediaLifecycleService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetOverallStorageStatsAsync_CallsLifecycleService()
        {
            // Arrange
            var expectedStats = new OverallMediaStorageStats
            {
                TotalSizeBytes = 100000,
                TotalFiles = 10,
                OrphanedFiles = 2
            };

            _mockMediaLifecycleService
                .Setup(s => s.GetOverallStorageStatsAsync())
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _service.GetOverallStorageStatsAsync();

            // Assert
            Assert.Equal(expectedStats, result);
            _mockMediaLifecycleService.Verify(s => s.GetOverallStorageStatsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetStorageStatsByVirtualKeyAsync_CallsLifecycleService()
        {
            // Arrange
            var virtualKeyId = 123;
            var expectedStats = new MediaStorageStats
            {
                VirtualKeyId = virtualKeyId,
                TotalFiles = 5,
                TotalSizeBytes = 50000
            };

            _mockMediaLifecycleService
                .Setup(s => s.GetStorageStatsByVirtualKeyAsync(virtualKeyId))
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _service.GetStorageStatsByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(expectedStats, result);
            _mockMediaLifecycleService.Verify(s => s.GetStorageStatsByVirtualKeyAsync(virtualKeyId), Times.Once);
        }

        [Fact]
        public async Task GetMediaByVirtualKeyAsync_CallsLifecycleService()
        {
            // Arrange
            var virtualKeyId = 123;
            var expectedMedia = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid(), VirtualKeyId = virtualKeyId }
            };

            _mockMediaLifecycleService
                .Setup(s => s.GetMediaByVirtualKeyAsync(virtualKeyId))
                .ReturnsAsync(expectedMedia);

            // Act
            var result = await _service.GetMediaByVirtualKeyAsync(virtualKeyId);

            // Assert
            Assert.Equal(expectedMedia, result);
            _mockMediaLifecycleService.Verify(s => s.GetMediaByVirtualKeyAsync(virtualKeyId), Times.Once);
        }

        [Fact]
        public async Task CleanupExpiredMediaAsync_ReturnsCleanupCount()
        {
            // Arrange
            var expectedCount = 5;

            _mockMediaLifecycleService
                .Setup(s => s.CleanupExpiredMediaAsync())
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _service.CleanupExpiredMediaAsync();

            // Assert
            Assert.Equal(expectedCount, result);
            _mockMediaLifecycleService.Verify(s => s.CleanupExpiredMediaAsync(), Times.Once);
        }

        [Fact]
        public async Task CleanupOrphanedMediaAsync_ReturnsCleanupCount()
        {
            // Arrange
            var expectedCount = 3;

            _mockMediaLifecycleService
                .Setup(s => s.CleanupOrphanedMediaAsync())
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _service.CleanupOrphanedMediaAsync();

            // Assert
            Assert.Equal(expectedCount, result);
            _mockMediaLifecycleService.Verify(s => s.CleanupOrphanedMediaAsync(), Times.Once);
        }

        [Theory]
        [InlineData(30)]
        [InlineData(90)]
        [InlineData(365)]
        public async Task PruneOldMediaAsync_ValidDays_ReturnsPruneCount(int daysToKeep)
        {
            // Arrange
            var expectedCount = 10;

            _mockMediaLifecycleService
                .Setup(s => s.PruneOldMediaAsync(daysToKeep, true))
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _service.PruneOldMediaAsync(daysToKeep);

            // Assert
            Assert.Equal(expectedCount, result);
            _mockMediaLifecycleService.Verify(s => s.PruneOldMediaAsync(daysToKeep, true), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task PruneOldMediaAsync_InvalidDays_ThrowsArgumentException(int invalidDays)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.PruneOldMediaAsync(invalidDays));
        }

        [Fact]
        public async Task DeleteMediaAsync_ExistingMedia_ReturnsTrue()
        {
            // Arrange
            var mediaId = Guid.NewGuid();
            var mediaRecord = new MediaRecord 
            { 
                Id = mediaId, 
                StorageKey = "test-key" 
            };

            _mockMediaRepository
                .Setup(r => r.GetByIdAsync(mediaId))
                .ReturnsAsync(mediaRecord);

            _mockMediaRepository
                .Setup(r => r.DeleteAsync(mediaId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMediaAsync(mediaId);

            // Assert
            Assert.True(result);
            _mockMediaRepository.Verify(r => r.DeleteAsync(mediaId), Times.Once);
        }

        [Fact]
        public async Task DeleteMediaAsync_NonExistentMedia_ReturnsFalse()
        {
            // Arrange
            var mediaId = Guid.NewGuid();

            _mockMediaRepository
                .Setup(r => r.GetByIdAsync(mediaId))
                .ReturnsAsync((MediaRecord)null);

            // Act
            var result = await _service.DeleteMediaAsync(mediaId);

            // Assert
            Assert.False(result);
            _mockMediaRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task SearchMediaByStorageKeyAsync_ValidPattern_ReturnsMatchingRecords()
        {
            // Arrange
            var pattern = "test";
            var allMedia = new List<MediaRecord>
            {
                new MediaRecord { StorageKey = "test-key-1", CreatedAt = DateTime.UtcNow },
                new MediaRecord { StorageKey = "test-key-2", CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new MediaRecord { StorageKey = "other-key", CreatedAt = DateTime.UtcNow }
            };

            _mockMediaRepository
                .Setup(r => r.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(allMedia);

            // Act
            var result = await _service.SearchMediaByStorageKeyAsync(pattern);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, m => Assert.Contains(pattern, m.StorageKey));
            Assert.Equal("test-key-1", result[0].StorageKey); // Should be ordered by CreatedAt desc
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task SearchMediaByStorageKeyAsync_EmptyPattern_ReturnsEmptyList(string emptyPattern)
        {
            // Act
            var result = await _service.SearchMediaByStorageKeyAsync(emptyPattern);

            // Assert
            Assert.Empty(result);
            _mockMediaRepository.Verify(r => r.GetMediaOlderThanAsync(It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task GetStorageStatsByProviderAsync_CallsRepository()
        {
            // Arrange
            var expectedStats = new Dictionary<string, long>
            {
                { "openai", 100000 },
                { "minimax", 50000 }
            };

            _mockMediaRepository
                .Setup(r => r.GetStorageStatsByProviderAsync())
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _service.GetStorageStatsByProviderAsync();

            // Assert
            Assert.Equal(expectedStats, result);
            _mockMediaRepository.Verify(r => r.GetStorageStatsByProviderAsync(), Times.Once);
        }

        [Fact]
        public async Task GetStorageStatsByMediaTypeAsync_CallsRepository()
        {
            // Arrange
            var expectedStats = new Dictionary<string, long>
            {
                { "image", 120000 },
                { "video", 30000 }
            };

            _mockMediaRepository
                .Setup(r => r.GetStorageStatsByMediaTypeAsync())
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _service.GetStorageStatsByMediaTypeAsync();

            // Assert
            Assert.Equal(expectedStats, result);
            _mockMediaRepository.Verify(r => r.GetStorageStatsByMediaTypeAsync(), Times.Once);
        }
    }
}