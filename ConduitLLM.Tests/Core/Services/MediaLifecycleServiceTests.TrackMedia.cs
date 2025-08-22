using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class MediaLifecycleServiceTests
    {
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
    }
}