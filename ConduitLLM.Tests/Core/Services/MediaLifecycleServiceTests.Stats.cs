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
    public partial class MediaLifecycleServiceTests
    {
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

            var allMedia = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid(), MediaType = "image", SizeBytes = 200000 },
                new MediaRecord { Id = Guid.NewGuid(), MediaType = "image", SizeBytes = 300000 },
                new MediaRecord { Id = Guid.NewGuid(), MediaType = "video", SizeBytes = 2500000 }
            };

            var orphanedMedia = new List<MediaRecord>
            {
                new MediaRecord { Id = Guid.NewGuid() }
            };

            _mockMediaRepository.Setup(x => x.GetStorageStatsByProviderAsync())
                .ReturnsAsync(byProvider);

            _mockMediaRepository.Setup(x => x.GetMediaOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(allMedia);

            _mockMediaRepository.Setup(x => x.GetOrphanedMediaAsync())
                .ReturnsAsync(orphanedMedia);

            // Act
            var result = await _service.GetOverallStorageStatsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3000000, result.TotalSizeBytes); // Sum of all media sizes
            Assert.Equal(3, result.TotalFiles);
            Assert.Equal(1, result.OrphanedFiles);
            Assert.Equal(byProvider, result.ByProvider);
            
            // Check byMediaType structure
            Assert.Equal(2, result.ByMediaType.Count);
            Assert.Equal(2, result.ByMediaType["image"].FileCount);
            Assert.Equal(500000, result.ByMediaType["image"].SizeBytes);
            Assert.Equal(1, result.ByMediaType["video"].FileCount);
            Assert.Equal(2500000, result.ByMediaType["video"].SizeBytes);
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