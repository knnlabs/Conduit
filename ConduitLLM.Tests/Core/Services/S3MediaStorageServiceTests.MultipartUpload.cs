using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Options;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class S3MediaStorageServiceTests
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
                GeneratedByModel = "test-model"
            };

            var initiateResponse = new InitiateMultipartUploadResponse
            {
                BucketName = _options.BucketName,
                Key = "video/test-key.mp4",
                UploadId = "test-upload-id"
            };

            _mockS3Client.Setup(x => x.InitiateMultipartUploadAsync(It.IsAny<InitiateMultipartUploadRequest>(), default))
                .ReturnsAsync(initiateResponse);

            // Act
            var session = await _service.InitiateMultipartUploadAsync(metadata);

            // Assert
            Assert.NotNull(session);
            Assert.NotNull(session.SessionId);
            Assert.NotNull(session.StorageKey);
            Assert.StartsWith("video/", session.StorageKey);
            Assert.EndsWith(".mp4", session.StorageKey);
            Assert.Equal(10 * 1024 * 1024, session.MinimumPartSize); // 10MB (default for R2 optimization)
            Assert.Equal(10000, session.MaxParts);
            Assert.Equal("test-upload-id", session.S3UploadId);

            // Verify initiate request
            _mockS3Client.Verify(x => x.InitiateMultipartUploadAsync(It.Is<InitiateMultipartUploadRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.ContentType == "video/mp4"
                // ServerSideEncryptionMethod removed for R2 compatibility
            ), default), Times.Once);
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

            var initiateResponse = new InitiateMultipartUploadResponse
            {
                BucketName = _options.BucketName,
                Key = "video/test-key.mp4",
                UploadId = "test-upload-id"
            };

            var uploadPartResponse = new UploadPartResponse
            {
                ETag = "part-etag",
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.InitiateMultipartUploadAsync(It.IsAny<InitiateMultipartUploadRequest>(), default))
                .ReturnsAsync(initiateResponse);

            _mockS3Client.Setup(x => x.UploadPartAsync(It.IsAny<UploadPartRequest>(), default))
                .ReturnsAsync(uploadPartResponse);

            var session = await _service.InitiateMultipartUploadAsync(metadata);
            var partContent = new MemoryStream(Encoding.UTF8.GetBytes("part 1 content"));

            // Act
            var result = await _service.UploadPartAsync(session.SessionId, 1, partContent);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.PartNumber);
            Assert.Equal("part-etag", result.ETag);
            Assert.Equal(partContent.Length, result.SizeBytes);

            // Verify upload part request
            _mockS3Client.Verify(x => x.UploadPartAsync(It.Is<UploadPartRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == "video/test-key.mp4" &&
                req.UploadId == "test-upload-id" &&
                req.PartNumber == 1
            ), default), Times.Once);
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
        public async Task CompleteMultipartUploadAsync_WithValidParts_ShouldComplete()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "multipart.mp4"
            };

            string capturedStorageKey = null;

            _mockS3Client.Setup(x => x.InitiateMultipartUploadAsync(It.IsAny<InitiateMultipartUploadRequest>(), default))
                .Returns<InitiateMultipartUploadRequest, CancellationToken>((req, ct) =>
                {
                    capturedStorageKey = req.Key;
                    var response = new InitiateMultipartUploadResponse
                    {
                        BucketName = _options.BucketName,
                        Key = req.Key,
                        UploadId = "test-upload-id"
                    };
                    return Task.FromResult(response);
                });

            _mockS3Client.Setup(x => x.CompleteMultipartUploadAsync(It.IsAny<CompleteMultipartUploadRequest>(), default))
                .Returns<CompleteMultipartUploadRequest, CancellationToken>((req, ct) =>
                {
                    var response = new CompleteMultipartUploadResponse
                    {
                        BucketName = _options.BucketName,
                        Key = req.Key,
                        ETag = "complete-etag",
                        Location = $"https://s3.amazonaws.com/{_options.BucketName}/{req.Key}"
                    };
                    return Task.FromResult(response);
                });

            var session = await _service.InitiateMultipartUploadAsync(metadata);
            var parts = new List<PartUploadResult>
            {
                new PartUploadResult { PartNumber = 1, ETag = "etag1", SizeBytes = 1000 },
                new PartUploadResult { PartNumber = 2, ETag = "etag2", SizeBytes = 2000 }
            };

            // Act
            var result = await _service.CompleteMultipartUploadAsync(session.SessionId, parts);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(session.StorageKey, result.StorageKey);
            Assert.Equal(3000, result.SizeBytes); // Sum of part sizes
            Assert.Equal("complete-etag", result.ContentHash);

            // Verify complete request
            _mockS3Client.Verify(x => x.CompleteMultipartUploadAsync(It.Is<CompleteMultipartUploadRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key.StartsWith("video/") &&
                req.Key.EndsWith(".mp4") &&
                req.UploadId == "test-upload-id" &&
                req.PartETags.Count == 2
            ), default), Times.Once);
        }

        [Fact]
        public async Task AbortMultipartUploadAsync_WithValidSession_ShouldAbort()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "multipart.mp4"
            };

            var initiateResponse = new InitiateMultipartUploadResponse
            {
                BucketName = _options.BucketName,
                Key = "video/test-key.mp4",
                UploadId = "test-upload-id"
            };

            var abortResponse = new AbortMultipartUploadResponse
            {
                HttpStatusCode = HttpStatusCode.NoContent
            };

            _mockS3Client.Setup(x => x.InitiateMultipartUploadAsync(It.IsAny<InitiateMultipartUploadRequest>(), default))
                .ReturnsAsync(initiateResponse);

            _mockS3Client.Setup(x => x.AbortMultipartUploadAsync(It.IsAny<AbortMultipartUploadRequest>(), default))
                .ReturnsAsync(abortResponse);

            var session = await _service.InitiateMultipartUploadAsync(metadata);

            // Act
            await _service.AbortMultipartUploadAsync(session.SessionId);

            // Assert
            _mockS3Client.Verify(x => x.AbortMultipartUploadAsync(It.Is<AbortMultipartUploadRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == "video/test-key.mp4" &&
                req.UploadId == "test-upload-id"
            ), default), Times.Once);

            // Verify session is removed - should throw exception when trying to use it
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UploadPartAsync(session.SessionId, 1, new MemoryStream()));
        }

        #endregion
    }
}