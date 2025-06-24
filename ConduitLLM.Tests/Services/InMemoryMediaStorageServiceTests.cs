using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for InMemoryMediaStorageService covering memory storage, concurrent access, and lifecycle management.
    /// </summary>
    public class InMemoryMediaStorageServiceTests : IDisposable
    {
        private readonly Mock<ILogger<InMemoryMediaStorageService>> _mockLogger;
        private readonly InMemoryMediaStorageService _service;
        private readonly string _testBaseUrl = "http://localhost:5000";

        public InMemoryMediaStorageServiceTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryMediaStorageService>>();
            _service = new InMemoryMediaStorageService(_mockLogger.Object, _testBaseUrl);
        }

        [Fact]
        public async Task StoreAsync_WithValidImageData_ReturnsStorageResult()
        {
            // Arrange
            var imageData = Encoding.UTF8.GetBytes("fake-image-data");
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CustomMetadata = new Dictionary<string, string> { ["tag"] = "test" }
            };

            using var stream = new MemoryStream(imageData);

            // Act
            var result = await _service.StoreAsync(stream, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.StorageKey);
            Assert.True(result.StorageKey.StartsWith("image/"));
            Assert.True(result.StorageKey.EndsWith(".jpg"));
            Assert.Equal(imageData.Length, result.SizeBytes);
            Assert.NotNull(result.ContentHash);
            Assert.NotNull(result.Url);
            Assert.True(result.Url.StartsWith(_testBaseUrl));
        }

        [Fact]
        public async Task StoreAsync_WithVideoData_StoresWithCorrectMediaType()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("fake-video-data");
            var metadata = new MediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4",
                MediaType = MediaType.Video
            };

            using var stream = new MemoryStream(videoData);

            // Act
            var result = await _service.StoreAsync(stream, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.StorageKey.StartsWith("video/"));
            Assert.True(result.StorageKey.EndsWith(".mp4"));
        }

        [Fact]
        public async Task StoreAsync_WithExpirationDate_StoresExpirationInMetadata()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("test-data");
            var expiresAt = DateTime.UtcNow.AddDays(7);
            var metadata = new MediaMetadata
            {
                ContentType = "image/png",
                FileName = "test.png",
                MediaType = MediaType.Image,
                ExpiresAt = expiresAt
            };

            using var stream = new MemoryStream(data);

            // Act
            var result = await _service.StoreAsync(stream, metadata);
            var info = await _service.GetInfoAsync(result.StorageKey);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(expiresAt, info.ExpiresAt);
        }

        [Fact]
        public async Task GetStreamAsync_WithExistingKey_ReturnsStream()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("test-stream-data");
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                MediaType = MediaType.Other
            };

            using var originalStream = new MemoryStream(originalData);
            var result = await _service.StoreAsync(originalStream, metadata);

            // Act
            var retrievedStream = await _service.GetStreamAsync(result.StorageKey);

            // Assert
            Assert.NotNull(retrievedStream);
            using (retrievedStream)
            {
                var retrievedData = new byte[originalData.Length];
                await retrievedStream.ReadAsync(retrievedData, 0, retrievedData.Length);
                Assert.Equal(originalData, retrievedData);
            }
        }

        [Fact]
        public async Task GetStreamAsync_WithNonExistentKey_ReturnsNull()
        {
            // Act
            var stream = await _service.GetStreamAsync("non-existent-key");

            // Assert
            Assert.Null(stream);
        }

        [Fact]
        public async Task GetInfoAsync_WithExistingKey_ReturnsMediaInfo()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("info-test-data");
            var metadata = new MediaMetadata
            {
                ContentType = "application/json",
                FileName = "data.json",
                MediaType = MediaType.Other,
                CustomMetadata = new Dictionary<string, string>
                {
                    ["purpose"] = "testing",
                    ["version"] = "1.0"
                }
            };

            using var stream = new MemoryStream(data);
            var result = await _service.StoreAsync(stream, metadata);

            // Act
            var info = await _service.GetInfoAsync(result.StorageKey);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(result.StorageKey, info.StorageKey);
            Assert.Equal("application/json", info.ContentType);
            Assert.Equal("data.json", info.FileName);
            Assert.Equal(MediaType.Other, info.MediaType);
            Assert.Equal(data.Length, info.SizeBytes);
            Assert.Equal("testing", info.CustomMetadata["purpose"]);
            Assert.Equal("1.0", info.CustomMetadata["version"]);
        }

        [Fact]
        public async Task GetInfoAsync_WithNonExistentKey_ReturnsNull()
        {
            // Act
            var info = await _service.GetInfoAsync("non-existent-key");

            // Assert
            Assert.Null(info);
        }

        [Fact]
        public async Task DeleteAsync_WithExistingKey_RemovesFromStorage()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("delete-test-data");
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                MediaType = MediaType.Other
            };

            using var stream = new MemoryStream(data);
            var result = await _service.StoreAsync(stream, metadata);

            // Verify it exists
            Assert.True(await _service.ExistsAsync(result.StorageKey));

            // Act
            var deleted = await _service.DeleteAsync(result.StorageKey);

            // Assert
            Assert.True(deleted);
            Assert.False(await _service.ExistsAsync(result.StorageKey));
            Assert.Null(await _service.GetStreamAsync(result.StorageKey));
            Assert.Null(await _service.GetInfoAsync(result.StorageKey));
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistentKey_ReturnsFalse()
        {
            // Act
            var deleted = await _service.DeleteAsync("non-existent-key");

            // Assert
            Assert.False(deleted);
        }

        [Fact]
        public async Task ExistsAsync_WithExistingKey_ReturnsTrue()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("exists-test-data");
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                MediaType = MediaType.Other
            };

            using var stream = new MemoryStream(data);
            var result = await _service.StoreAsync(stream, metadata);

            // Act
            var exists = await _service.ExistsAsync(result.StorageKey);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistentKey_ReturnsFalse()
        {
            // Act
            var exists = await _service.ExistsAsync("non-existent-key");

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task GenerateUrlAsync_WithValidKey_ReturnsExpectedUrl()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("url-test-data");
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                MediaType = MediaType.Other
            };

            using var stream = new MemoryStream(data);
            var result = await _service.StoreAsync(stream, metadata);

            // Act
            var url = await _service.GenerateUrlAsync(result.StorageKey);

            // Assert
            Assert.NotNull(url);
            Assert.StartsWith($"{_testBaseUrl}/v1/media/", url);
            Assert.EndsWith(result.StorageKey, url);
        }

        [Fact]
        public async Task GenerateUrlAsync_WithExpiration_IgnoresExpirationForInMemory()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("url-expiration-test-data");
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                MediaType = MediaType.Other
            };

            using var stream = new MemoryStream(data);
            var result = await _service.StoreAsync(stream, metadata);

            // Act
            var url1 = await _service.GenerateUrlAsync(result.StorageKey);
            var url2 = await _service.GenerateUrlAsync(result.StorageKey, TimeSpan.FromHours(1));

            // Assert
            Assert.Equal(url1, url2); // In-memory implementation ignores expiration
        }

        [Fact]
        public void GetTotalSizeBytes_WithMultipleFiles_ReturnsCorrectTotal()
        {
            // Arrange
            var data1 = new byte[100];
            var data2 = new byte[200];
            var data3 = new byte[300];

            var metadata = new MediaMetadata
            {
                ContentType = "application/octet-stream",
                MediaType = MediaType.Other
            };

            // Act
            using (var stream1 = new MemoryStream(data1))
                _service.StoreAsync(stream1, metadata).Wait();
            
            using (var stream2 = new MemoryStream(data2))
                _service.StoreAsync(stream2, metadata).Wait();
            
            using (var stream3 = new MemoryStream(data3))
                _service.StoreAsync(stream3, metadata).Wait();

            var totalSize = _service.GetTotalSizeBytes();

            // Assert
            Assert.Equal(600, totalSize); // 100 + 200 + 300
        }

        [Fact]
        public void GetItemCount_WithMultipleFiles_ReturnsCorrectCount()
        {
            // Arrange
            var metadata = new MediaMetadata
            {
                ContentType = "application/octet-stream",
                MediaType = MediaType.Other
            };

            // Act - Create files with different content to ensure different hashes
            for (int i = 0; i < 5; i++)
            {
                var data = Encoding.UTF8.GetBytes($"unique-data-{i}");
                using var stream = new MemoryStream(data);
                _service.StoreAsync(stream, metadata).Wait();
            }

            var itemCount = _service.GetItemCount();

            // Assert
            Assert.Equal(5, itemCount);
        }

        [Fact]
        public async Task StoreVideoAsync_WithProgressCallback_ReportsProgress()
        {
            // Arrange
            var videoData = new byte[1024]; // 1KB
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4",
                Duration = 10.5,
                Resolution = "1920x1080",
                Width = 1920,
                Height = 1080,
                FrameRate = 30.0
            };

            var progressReports = new List<long>();
            using var stream = new MemoryStream(videoData);

            // Act
            var result = await _service.StoreVideoAsync(stream, metadata, progress => progressReports.Add(progress));

            // Assert
            Assert.NotNull(result);
            Assert.True(progressReports.Count > 0);
            Assert.Equal(videoData.Length, progressReports[^1]); // Last progress should be total size

            // Verify video metadata was stored
            var info = await _service.GetInfoAsync(result.StorageKey);
            Assert.NotNull(info);
            Assert.Equal("10.5", info.CustomMetadata["duration"]);
            Assert.Equal("1920x1080", info.CustomMetadata["resolution"]);
            Assert.Equal("1920", info.CustomMetadata["width"]);
            Assert.Equal("1080", info.CustomMetadata["height"]);
            Assert.Equal("30", info.CustomMetadata["framerate"]);
        }

        [Fact]
        public async Task InitiateMultipartUploadAsync_ReturnsValidSession()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "large-video.mp4",
                Duration = 60.0,
                Resolution = "4096x2160"
            };

            // Act
            var session = await _service.InitiateMultipartUploadAsync(metadata);

            // Assert
            Assert.NotNull(session);
            Assert.NotNull(session.SessionId);
            Assert.NotNull(session.StorageKey);
            Assert.True(session.StorageKey.StartsWith("video/"));
            Assert.Equal(1024 * 1024, session.MinimumPartSize); // 1MB for in-memory
            Assert.Equal(1000, session.MaxParts);
            Assert.True(session.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task UploadPartAsync_WithValidSession_StoresPart()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                Duration = 60.0
            };

            var session = await _service.InitiateMultipartUploadAsync(metadata);
            var partData = Encoding.UTF8.GetBytes("part-1-data");

            using var partStream = new MemoryStream(partData);

            // Act
            var result = await _service.UploadPartAsync(session.SessionId, 1, partStream);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.PartNumber);
            Assert.NotNull(result.ETag);
            Assert.Equal(partData.Length, result.SizeBytes);
        }

        [Fact]
        public async Task UploadPartAsync_WithInvalidSession_ThrowsException()
        {
            // Arrange
            var partData = Encoding.UTF8.GetBytes("part-data");
            using var partStream = new MemoryStream(partData);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UploadPartAsync("invalid-session", 1, partStream));
        }

        [Fact]
        public async Task CompleteMultipartUploadAsync_WithValidParts_CombinesPartsCorrectly()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                Duration = 60.0
            };

            var session = await _service.InitiateMultipartUploadAsync(metadata);
            var part1Data = Encoding.UTF8.GetBytes("part-1-");
            var part2Data = Encoding.UTF8.GetBytes("part-2-");
            var part3Data = Encoding.UTF8.GetBytes("part-3");

            var parts = new List<PartUploadResult>();

            // Upload parts
            using (var stream1 = new MemoryStream(part1Data))
                parts.Add(await _service.UploadPartAsync(session.SessionId, 1, stream1));
            
            using (var stream2 = new MemoryStream(part2Data))
                parts.Add(await _service.UploadPartAsync(session.SessionId, 2, stream2));
            
            using (var stream3 = new MemoryStream(part3Data))
                parts.Add(await _service.UploadPartAsync(session.SessionId, 3, stream3));

            // Act
            var result = await _service.CompleteMultipartUploadAsync(session.SessionId, parts);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(session.StorageKey, result.StorageKey);
            Assert.Equal(part1Data.Length + part2Data.Length + part3Data.Length, result.SizeBytes);

            // Verify combined content
            var stream = await _service.GetStreamAsync(result.StorageKey);
            Assert.NotNull(stream);
            
            using (stream)
            {
                var combinedData = new byte[result.SizeBytes];
                await stream.ReadAsync(combinedData, 0, combinedData.Length);
                var expectedData = Encoding.UTF8.GetBytes("part-1-part-2-part-3");
                Assert.Equal(expectedData, combinedData);
            }
        }

        [Fact]
        public async Task AbortMultipartUploadAsync_RemovesSession()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                Duration = 60.0
            };

            var session = await _service.InitiateMultipartUploadAsync(metadata);

            // Act
            await _service.AbortMultipartUploadAsync(session.SessionId);

            // Assert
            // Verify session is removed by trying to upload a part
            var partData = Encoding.UTF8.GetBytes("part-data");
            using var partStream = new MemoryStream(partData);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UploadPartAsync(session.SessionId, 1, partStream));
        }

        [Fact]
        public async Task GetVideoStreamAsync_WithRange_ReturnsCorrectRange()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("0123456789"); // 10 bytes
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                Duration = 10.0
            };

            using var stream = new MemoryStream(videoData);
            var result = await _service.StoreVideoAsync(stream, metadata);

            // Act - Request bytes 2-5
            var rangedStream = await _service.GetVideoStreamAsync(result.StorageKey, 2, 5);

            // Assert
            Assert.NotNull(rangedStream);
            Assert.Equal(2, rangedStream.RangeStart);
            Assert.Equal(5, rangedStream.RangeEnd);
            Assert.Equal(10, rangedStream.TotalSize);
            Assert.Equal("video/mp4", rangedStream.ContentType);

            using (rangedStream.Stream)
            {
                var rangedData = new byte[4]; // bytes 2-5 = 4 bytes
                await rangedStream.Stream.ReadAsync(rangedData, 0, rangedData.Length);
                var expectedData = Encoding.UTF8.GetBytes("2345");
                Assert.Equal(expectedData, rangedData);
            }
        }

        [Fact]
        public async Task GetVideoStreamAsync_WithoutRange_ReturnsFullVideo()
        {
            // Arrange
            var videoData = Encoding.UTF8.GetBytes("full-video-data");
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                Duration = 30.0
            };

            using var stream = new MemoryStream(videoData);
            var result = await _service.StoreVideoAsync(stream, metadata);

            // Act
            var rangedStream = await _service.GetVideoStreamAsync(result.StorageKey);

            // Assert
            Assert.NotNull(rangedStream);
            Assert.Equal(0, rangedStream.RangeStart);
            Assert.Equal(videoData.Length - 1, rangedStream.RangeEnd);
            Assert.Equal(videoData.Length, rangedStream.TotalSize);

            using (rangedStream.Stream)
            {
                var fullData = new byte[videoData.Length];
                await rangedStream.Stream.ReadAsync(fullData, 0, fullData.Length);
                Assert.Equal(videoData, fullData);
            }
        }

        [Fact]
        public async Task GeneratePresignedUploadUrlAsync_ReturnsValidUrl()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "upload-test.mp4",
                Duration = 15.0
            };

            var expiration = TimeSpan.FromHours(1);

            // Act
            var presignedUrl = await _service.GeneratePresignedUploadUrlAsync(metadata, expiration);

            // Assert
            Assert.NotNull(presignedUrl);
            Assert.NotNull(presignedUrl.Url);
            Assert.True(presignedUrl.Url.StartsWith($"{_testBaseUrl}/v1/media/upload/"));
            Assert.Equal("PUT", presignedUrl.HttpMethod);
            Assert.Equal("video/mp4", presignedUrl.RequiredHeaders["Content-Type"]);
            Assert.NotNull(presignedUrl.StorageKey);
            Assert.True(presignedUrl.StorageKey.StartsWith("video/"));
            Assert.Equal(100 * 1024 * 1024, presignedUrl.MaxFileSizeBytes); // 100MB
            Assert.True(presignedUrl.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task ConcurrentAccess_MultipleStoreOperations_HandledCorrectly()
        {
            // Arrange
            var tasks = new List<Task<MediaStorageResult>>();
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                MediaType = MediaType.Other
            };

            // Act - Perform concurrent store operations
            for (int i = 0; i < 10; i++)
            {
                var data = Encoding.UTF8.GetBytes($"concurrent-data-{i}");
                var stream = new MemoryStream(data);
                tasks.Add(_service.StoreAsync(stream, metadata));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(10, results.Length);
            Assert.Equal(10, _service.GetItemCount());

            // Verify all storage keys are unique
            var storageKeys = new HashSet<string>();
            foreach (var result in results)
            {
                Assert.True(storageKeys.Add(result.StorageKey), $"Duplicate storage key: {result.StorageKey}");
            }
        }

        [Fact]
        public async Task ConcurrentAccess_StoreAndRetrieveOperations_WorkCorrectly()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("concurrent-test-data");
            var metadata = new MediaMetadata
            {
                ContentType = "text/plain",
                MediaType = MediaType.Other
            };

            using var stream = new MemoryStream(data);
            var result = await _service.StoreAsync(stream, metadata);

            // Act - Perform concurrent retrieve operations
            var retrieveTasks = new List<Task<Stream?>>();
            for (int i = 0; i < 5; i++)
            {
                retrieveTasks.Add(_service.GetStreamAsync(result.StorageKey));
            }

            var streams = await Task.WhenAll(retrieveTasks);

            // Assert
            Assert.Equal(5, streams.Length);
            foreach (var retrievedStream in streams)
            {
                Assert.NotNull(retrievedStream);
                using (retrievedStream)
                {
                    var retrievedData = new byte[data.Length];
                    await retrievedStream.ReadAsync(retrievedData, 0, retrievedData.Length);
                    Assert.Equal(data, retrievedData);
                }
            }
        }

        public void Dispose()
        {
            // Cleanup any resources if needed
        }
    }
}