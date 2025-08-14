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
        #region StoreVideoAsync Tests

        [Fact]
        public async Task StoreVideoAsync_WithValidData_ShouldStoreSuccessfully()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("fake video content"));
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4",
                Duration = 30,
                Resolution = "1920x1080",
                Width = 1920,
                Height = 1080,
                FrameRate = 30.0,
                CustomMetadata = new Dictionary<string, string> { ["source"] = "test" }
            };

            var progressCallbacks = new List<long>();

            // Act
            var result = await _service.StoreVideoAsync(content, metadata, progressCallbacks.Add);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("video/", result.StorageKey);
            Assert.EndsWith(".mp4", result.StorageKey);
            Assert.Equal(18, result.SizeBytes); // "fake video content" length
            Assert.NotNull(result.ContentHash);
            Assert.NotNull(result.Url);
            Assert.NotEmpty(progressCallbacks);
        }

        [Fact]
        public async Task StoreVideoAsync_WithProgressCallback_ShouldReportProgress()
        {
            // Arrange
            var largeContent = new MemoryStream(new byte[200000]); // 200KB
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "large.mp4",
                Duration = 60,
                Resolution = "1920x1080",
                Width = 1920,
                Height = 1080,
                FrameRate = 24.0
            };

            var progressCallbacks = new List<long>();

            // Act
            var result = await _service.StoreVideoAsync(largeContent, metadata, progressCallbacks.Add);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(progressCallbacks);
            Assert.Equal(200000, progressCallbacks.Last());
        }

        [Fact]
        public async Task StoreVideoAsync_WithoutProgressCallback_ShouldStoreSuccessfully()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("video content"));
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/webm",
                FileName = "test.webm",
                Duration = 15,
                Resolution = "640x480",
                Width = 640,
                Height = 480,
                FrameRate = 30.0
            };

            // Act
            var result = await _service.StoreVideoAsync(content, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("video/", result.StorageKey);
            Assert.EndsWith(".webm", result.StorageKey);
        }

        #endregion

        #region GetVideoStreamAsync Tests

        [Fact]
        public async Task GetVideoStreamAsync_WithExistingVideo_ShouldReturnFullStream()
        {
            // Arrange
            var videoContent = "full video content data";
            var content = new MemoryStream(Encoding.UTF8.GetBytes(videoContent));
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4"
            };

            var storeResult = await _service.StoreVideoAsync(content, metadata);

            // Act
            var rangedStream = await _service.GetVideoStreamAsync(storeResult.StorageKey);

            // Assert
            Assert.NotNull(rangedStream);
            Assert.Equal(0, rangedStream.RangeStart);
            Assert.Equal(videoContent.Length - 1, rangedStream.RangeEnd);
            Assert.Equal(videoContent.Length, rangedStream.TotalSize);
            Assert.Equal("video/mp4", rangedStream.ContentType);

            using var reader = new StreamReader(rangedStream.Stream);
            var retrievedContent = await reader.ReadToEndAsync();
            Assert.Equal(videoContent, retrievedContent);
        }

        [Fact]
        public async Task GetVideoStreamAsync_WithRangeRequest_ShouldReturnPartialStream()
        {
            // Arrange
            var videoContent = "0123456789abcdef";
            var content = new MemoryStream(Encoding.UTF8.GetBytes(videoContent));
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4"
            };

            var storeResult = await _service.StoreVideoAsync(content, metadata);

            // Act
            var rangedStream = await _service.GetVideoStreamAsync(storeResult.StorageKey, 5, 10);

            // Assert
            Assert.NotNull(rangedStream);
            Assert.Equal(5, rangedStream.RangeStart);
            Assert.Equal(10, rangedStream.RangeEnd);
            Assert.Equal(videoContent.Length, rangedStream.TotalSize);

            using var reader = new StreamReader(rangedStream.Stream);
            var retrievedContent = await reader.ReadToEndAsync();
            Assert.Equal("56789a", retrievedContent); // Characters 5-10 inclusive
        }

        [Fact]
        public async Task GetVideoStreamAsync_WithInvalidRange_ShouldClampToValidRange()
        {
            // Arrange
            var videoContent = "short";
            var content = new MemoryStream(Encoding.UTF8.GetBytes(videoContent));
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4"
            };

            var storeResult = await _service.StoreVideoAsync(content, metadata);

            // Act
            var rangedStream = await _service.GetVideoStreamAsync(storeResult.StorageKey, -5, 100);

            // Assert
            Assert.NotNull(rangedStream);
            Assert.Equal(0, rangedStream.RangeStart);
            Assert.Equal(4, rangedStream.RangeEnd); // "short" is 5 chars, so 0-4
            Assert.Equal(5, rangedStream.TotalSize);
        }

        [Fact]
        public async Task GetVideoStreamAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Act
            var rangedStream = await _service.GetVideoStreamAsync("non-existent-key");

            // Assert
            Assert.Null(rangedStream);
        }

        #endregion

        #region GeneratePresignedUploadUrlAsync Tests

        [Fact]
        public async Task GeneratePresignedUploadUrlAsync_ShouldReturnValidUrl()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "upload.mp4"
            };
            var expiration = TimeSpan.FromHours(1);

            // Act
            var presignedUrl = await _service.GeneratePresignedUploadUrlAsync(metadata, expiration);

            // Assert
            Assert.NotNull(presignedUrl);
            Assert.StartsWith($"{TestBaseUrl}/v1/media/upload/", presignedUrl.Url);
            Assert.Equal("PUT", presignedUrl.HttpMethod);
            Assert.Contains("Content-Type", presignedUrl.RequiredHeaders.Keys);
            Assert.Equal("video/mp4", presignedUrl.RequiredHeaders["Content-Type"]);
            Assert.True(presignedUrl.ExpiresAt > DateTime.UtcNow);
            Assert.Equal(100 * 1024 * 1024, presignedUrl.MaxFileSizeBytes); // 100MB
        }

        #endregion
    }
}