using System.Text;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class InMemoryMediaStorageServiceTests
    {
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
    }
}