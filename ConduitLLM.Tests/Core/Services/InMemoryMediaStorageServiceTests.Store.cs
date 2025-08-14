using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class InMemoryMediaStorageServiceTests
    {
        #region StoreAsync Tests

        [Fact]
        public async Task StoreAsync_WithValidImageData_ShouldStoreSuccessfully()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("fake image data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                CustomMetadata = new Dictionary<string, string> { ["source"] = "test" }
            };

            // Act
            var result = await _service.StoreAsync(content, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.StorageKey);
            Assert.StartsWith("image/", result.StorageKey);
            Assert.EndsWith(".jpg", result.StorageKey);
            Assert.Equal(15, result.SizeBytes); // "fake image data" length
            Assert.NotNull(result.ContentHash);
            Assert.NotNull(result.Url);
            Assert.Contains("/v1/media/", result.Url);
        }

        [Fact]
        public async Task StoreAsync_WithValidVideoData_ShouldStoreSuccessfully()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("fake video data"));
            var metadata = new MediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4",
                MediaType = MediaType.Video,
                CustomMetadata = new Dictionary<string, string> { ["duration"] = "30" }
            };

            // Act
            var result = await _service.StoreAsync(content, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("video/", result.StorageKey);
            Assert.EndsWith(".mp4", result.StorageKey);
            Assert.Equal(15, result.SizeBytes); // "fake video data" length
        }

        [Fact]
        public async Task StoreAsync_WithEmptyContent_ShouldStoreEmptyFile()
        {
            // Arrange
            var content = new MemoryStream();
            var metadata = new MediaMetadata
            {
                ContentType = "image/png",
                FileName = "empty.png",
                MediaType = MediaType.Image
            };

            // Act
            var result = await _service.StoreAsync(content, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.SizeBytes);
            Assert.NotNull(result.StorageKey);
        }

        [Fact]
        public async Task StoreAsync_WithNullMetadata_ShouldThrowException()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test"));

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(() => _service.StoreAsync(content, null));
        }

        [Fact]
        public async Task StoreAsync_WithSameContent_ShouldGenerateSameStorageKey()
        {
            // Arrange
            var content1 = new MemoryStream(Encoding.UTF8.GetBytes("identical content"));
            var content2 = new MemoryStream(Encoding.UTF8.GetBytes("identical content"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            // Act
            var result1 = await _service.StoreAsync(content1, metadata);
            var result2 = await _service.StoreAsync(content2, metadata);

            // Assert
            Assert.Equal(result1.StorageKey, result2.StorageKey);
            Assert.Equal(result1.ContentHash, result2.ContentHash);
        }

        #endregion
    }
}