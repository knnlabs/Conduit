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
    public class InMemoryMediaStorageServiceTests
    {
        private readonly Mock<ILogger<InMemoryMediaStorageService>> _mockLogger;
        private readonly InMemoryMediaStorageService _service;
        private const string TestBaseUrl = "http://localhost:5000";

        public InMemoryMediaStorageServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryMediaStorageService>>();
            _service = new InMemoryMediaStorageService(_mockLogger.Object, TestBaseUrl);
        }

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

        #region GetStreamAsync Tests

        [Fact]
        public async Task GetStreamAsync_WithExistingKey_ShouldReturnStream()
        {
            // Arrange
            var testData = "test data";
            var content = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var stream = await _service.GetStreamAsync(storeResult.StorageKey);

            // Assert
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream);
            var retrievedData = await reader.ReadToEndAsync();
            Assert.Equal(testData, retrievedData);
        }

        [Fact]
        public async Task GetStreamAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Act
            var stream = await _service.GetStreamAsync("non-existent-key");

            // Assert
            Assert.Null(stream);
        }

        [Fact]
        public async Task GetStreamAsync_WithNullKey_ShouldReturnNull()
        {
            // Act
            var stream = await _service.GetStreamAsync(null);

            // Assert
            Assert.Null(stream);
        }

        [Fact]
        public async Task GetStreamAsync_WithEmptyKey_ShouldReturnNull()
        {
            // Act
            var stream = await _service.GetStreamAsync("");

            // Assert
            Assert.Null(stream);
        }

        #endregion

        #region GetInfoAsync Tests

        [Fact]
        public async Task GetInfoAsync_WithExistingKey_ShouldReturnMediaInfo()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                CustomMetadata = new Dictionary<string, string> { ["test"] = "value" }
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var info = await _service.GetInfoAsync(storeResult.StorageKey);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(storeResult.StorageKey, info.StorageKey);
            Assert.Equal("image/jpeg", info.ContentType);
            Assert.Equal("test.jpg", info.FileName);
            Assert.Equal(MediaType.Image, info.MediaType);
            Assert.Equal(9, info.SizeBytes); // "test data" length
            Assert.NotNull(info.ExpiresAt);
            Assert.Contains("test", info.CustomMetadata.Keys);
            Assert.Equal("value", info.CustomMetadata["test"]);
        }

        [Fact]
        public async Task GetInfoAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Act
            var info = await _service.GetInfoAsync("non-existent-key");

            // Assert
            Assert.Null(info);
        }

        [Fact]
        public async Task GetInfoAsync_ShouldReturnCopyOfMetadata()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CustomMetadata = new Dictionary<string, string> { ["test"] = "value" }
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var info1 = await _service.GetInfoAsync(storeResult.StorageKey);
            var info2 = await _service.GetInfoAsync(storeResult.StorageKey);

            // Assert
            Assert.NotSame(info1, info2); // Different instances
            Assert.NotSame(info1.CustomMetadata, info2.CustomMetadata); // Different dictionaries
            Assert.Equal(info1.StorageKey, info2.StorageKey);
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_WithExistingKey_ShouldDeleteAndReturnTrue()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var deleted = await _service.DeleteAsync(storeResult.StorageKey);

            // Assert
            Assert.True(deleted);
            
            // Verify file is actually deleted
            var stream = await _service.GetStreamAsync(storeResult.StorageKey);
            Assert.Null(stream);
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistentKey_ShouldReturnFalse()
        {
            // Act
            var deleted = await _service.DeleteAsync("non-existent-key");

            // Assert
            Assert.False(deleted);
        }

        [Fact]
        public async Task DeleteAsync_WithNullKey_ShouldReturnFalse()
        {
            // Act
            var deleted = await _service.DeleteAsync(null);

            // Assert
            Assert.False(deleted);
        }

        #endregion

        #region GenerateUrlAsync Tests

        [Fact]
        public async Task GenerateUrlAsync_WithValidKey_ShouldReturnCorrectUrl()
        {
            // Arrange
            var storageKey = "image/test-hash.jpg";

            // Act
            var url = await _service.GenerateUrlAsync(storageKey);

            // Assert
            Assert.Equal($"{TestBaseUrl}/v1/media/{storageKey}", url);
        }

        [Fact]
        public async Task GenerateUrlAsync_WithExpiration_ShouldReturnUrl()
        {
            // Arrange
            var storageKey = "video/test-hash.mp4";
            var expiration = TimeSpan.FromHours(1);

            // Act
            var url = await _service.GenerateUrlAsync(storageKey, expiration);

            // Assert
            Assert.Equal($"{TestBaseUrl}/v1/media/{storageKey}", url);
            // Note: In-memory storage doesn't use expiration, but method should still work
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WithExistingKey_ShouldReturnTrue()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            var storeResult = await _service.StoreAsync(content, metadata);

            // Act
            var exists = await _service.ExistsAsync(storeResult.StorageKey);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistentKey_ShouldReturnFalse()
        {
            // Act
            var exists = await _service.ExistsAsync("non-existent-key");

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task ExistsAsync_WithDeletedKey_ShouldReturnFalse()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            var storeResult = await _service.StoreAsync(content, metadata);
            await _service.DeleteAsync(storeResult.StorageKey);

            // Act
            var exists = await _service.ExistsAsync(storeResult.StorageKey);

            // Assert
            Assert.False(exists);
        }

        #endregion

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

        #region Multipart Upload Tests

        [Fact]
        public async Task InitiateMultipartUploadAsync_ShouldCreateSession()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "multipart.mp4",
                Duration = 300,
                Resolution = "1920x1080",
                Width = 1920,
                Height = 1080,
                FrameRate = 30.0
            };

            // Act
            var session = await _service.InitiateMultipartUploadAsync(metadata);

            // Assert
            Assert.NotNull(session);
            Assert.NotNull(session.SessionId);
            Assert.NotNull(session.StorageKey);
            Assert.StartsWith("video/", session.StorageKey);
            Assert.EndsWith(".mp4", session.StorageKey);
            Assert.Equal(1024 * 1024, session.MinimumPartSize); // 1MB
            Assert.Equal(1000, session.MaxParts);
            Assert.True(session.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task UploadPartAsync_WithValidSession_ShouldUploadPart()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "multipart.mp4"
            };

            var session = await _service.InitiateMultipartUploadAsync(metadata);
            var partContent = new MemoryStream(Encoding.UTF8.GetBytes("part 1 content"));

            // Act
            var result = await _service.UploadPartAsync(session.SessionId, 1, partContent);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.PartNumber);
            Assert.Equal(14, result.SizeBytes); // "part 1 content" length
            Assert.NotNull(result.ETag);
        }

        [Fact]
        public async Task UploadPartAsync_WithInvalidSession_ShouldThrowException()
        {
            // Arrange
            var partContent = new MemoryStream(Encoding.UTF8.GetBytes("content"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UploadPartAsync("invalid-session", 1, partContent));
        }

        [Fact]
        public async Task CompleteMultipartUploadAsync_WithValidParts_ShouldCombineAndStore()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "multipart.mp4"
            };

            var session = await _service.InitiateMultipartUploadAsync(metadata);
            
            // Upload parts
            var part1Content = new MemoryStream(Encoding.UTF8.GetBytes("part 1 "));
            var part2Content = new MemoryStream(Encoding.UTF8.GetBytes("part 2 "));
            var part3Content = new MemoryStream(Encoding.UTF8.GetBytes("part 3"));

            var part1Result = await _service.UploadPartAsync(session.SessionId, 1, part1Content);
            var part2Result = await _service.UploadPartAsync(session.SessionId, 2, part2Content);
            var part3Result = await _service.UploadPartAsync(session.SessionId, 3, part3Content);

            var parts = new List<PartUploadResult> { part1Result, part2Result, part3Result };

            // Act
            var result = await _service.CompleteMultipartUploadAsync(session.SessionId, parts);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(session.StorageKey, result.StorageKey);
            Assert.Equal(20, result.SizeBytes); // "part 1 part 2 part 3" length
            Assert.NotNull(result.ContentHash);

            // Verify the combined content
            var stream = await _service.GetStreamAsync(result.StorageKey);
            using var reader = new StreamReader(stream);
            var combinedContent = await reader.ReadToEndAsync();
            Assert.Equal("part 1 part 2 part 3", combinedContent);
        }

        [Fact]
        public async Task CompleteMultipartUploadAsync_WithInvalidSession_ShouldThrowException()
        {
            // Arrange
            var parts = new List<PartUploadResult>
            {
                new PartUploadResult { PartNumber = 1, ETag = "etag1", SizeBytes = 100 }
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CompleteMultipartUploadAsync("invalid-session", parts));
        }

        [Fact]
        public async Task AbortMultipartUploadAsync_WithValidSession_ShouldCleanup()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "multipart.mp4"
            };

            var session = await _service.InitiateMultipartUploadAsync(metadata);

            // Act
            await _service.AbortMultipartUploadAsync(session.SessionId);

            // Assert - Should not throw when trying to upload to aborted session
            var partContent = new MemoryStream(Encoding.UTF8.GetBytes("content"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UploadPartAsync(session.SessionId, 1, partContent));
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

        #region Utility Method Tests

        [Fact]
        public async Task GetTotalSizeBytes_WithStoredFiles_ShouldReturnCorrectTotal()
        {
            // Arrange
            var content1 = new MemoryStream(Encoding.UTF8.GetBytes("file1"));
            var content2 = new MemoryStream(Encoding.UTF8.GetBytes("file2data"));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            // Act
            await _service.StoreAsync(content1, metadata);
            await _service.StoreAsync(content2, metadata);

            var totalSize = _service.GetTotalSizeBytes();

            // Assert
            Assert.Equal(14, totalSize); // 5 + 9 bytes
        }

        [Fact]
        public async Task GetItemCount_WithStoredFiles_ShouldReturnCorrectCount()
        {
            // Arrange
            var content1 = new MemoryStream(Encoding.UTF8.GetBytes("file1"));
            var content2 = new MemoryStream(Encoding.UTF8.GetBytes("file2"));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            // Act
            await _service.StoreAsync(content1, metadata);
            await _service.StoreAsync(content2, metadata);

            var itemCount = _service.GetItemCount();

            // Assert
            Assert.Equal(2, itemCount);
        }

        [Fact]
        public async Task GetItemCount_AfterDeletion_ShouldReturnCorrectCount()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("file"));
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                FileName = "test.txt",
                MediaType = MediaType.Other
            };

            // Act
            var storeResult = await _service.StoreAsync(content, metadata);
            var initialCount = _service.GetItemCount();
            await _service.DeleteAsync(storeResult.StorageKey);
            var finalCount = _service.GetItemCount();

            // Assert
            Assert.Equal(1, initialCount);
            Assert.Equal(0, finalCount);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public async Task Constructor_WithCustomBaseUrl_ShouldTrimTrailingSlash()
        {
            // Arrange
            var baseUrlWithSlash = "http://localhost:8080/";
            var service = new InMemoryMediaStorageService(_mockLogger.Object, baseUrlWithSlash);

            // Act
            var url = await service.GenerateUrlAsync("test/key");

            // Assert
            Assert.Equal("http://localhost:8080/v1/media/test/key", url);
        }

        [Fact]
        public async Task Constructor_WithDefaultBaseUrl_ShouldUseDefault()
        {
            // Arrange & Act
            var service = new InMemoryMediaStorageService(_mockLogger.Object);
            var url = await service.GenerateUrlAsync("test/key");

            // Assert
            Assert.Equal("http://localhost:5000/v1/media/test/key", url);
        }

        #endregion
    }
}