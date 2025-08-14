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
        #region StoreVideoAsync Tests

        [Fact(Skip = "S3 SDK TransferUtility cannot be easily mocked - requires integration test")]
        public async Task StoreVideoAsync_WithSmallVideo_ShouldUseRegularUpload()
        {
            // Arrange
            var videoContent = new byte[4 * 1024 * 1024]; // 4MB (under 5MB threshold for PutObject)
            var content = new MemoryStream(videoContent);
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "test.mp4",
                Duration = 30,
                Resolution = "1920x1080",
                Width = 1920,
                Height = 1080,
                FrameRate = 30.0,
                Codec = "h264",
                Bitrate = 5000000,
                GeneratedByModel = "test-model",
                GenerationPrompt = "test prompt"
            };

            var progressCallbacks = new List<long>();
            var putObjectResponse = new PutObjectResponse
            {
                ETag = "test-etag",
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                .ReturnsAsync(putObjectResponse);

            // Act
            var result = await _service.StoreVideoAsync(content, metadata, progressCallbacks.Add);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("video/", result.StorageKey);
            Assert.EndsWith(".mp4", result.StorageKey);
            Assert.Equal(videoContent.Length, result.SizeBytes);
            Assert.NotEmpty(progressCallbacks);

            // Verify put object was called for video upload
            _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
        }

        [Fact]
        public async Task StoreVideoAsync_WithLargeVideo_ShouldUseMultipartUpload()
        {
            // Arrange
            var videoContent = new byte[150 * 1024 * 1024]; // 150MB (over 100MB threshold)
            var content = new MemoryStream(videoContent);
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "large.mp4",
                Duration = 300,
                Resolution = "1920x1080",
                Width = 1920,
                Height = 1080,
                FrameRate = 30.0
            };

            var progressCallbacks = new List<long>();

            // Setup multipart upload mocks
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

            var completeResponse = new CompleteMultipartUploadResponse
            {
                BucketName = _options.BucketName,
                Key = "video/test-key.mp4",
                ETag = "complete-etag",
                Location = "https://s3.amazonaws.com/test-bucket/video/test-key.mp4"
            };

            _mockS3Client.Setup(x => x.InitiateMultipartUploadAsync(It.IsAny<InitiateMultipartUploadRequest>(), default))
                .ReturnsAsync(initiateResponse);

            _mockS3Client.Setup(x => x.UploadPartAsync(It.IsAny<UploadPartRequest>(), default))
                .ReturnsAsync(uploadPartResponse);

            _mockS3Client.Setup(x => x.CompleteMultipartUploadAsync(It.IsAny<CompleteMultipartUploadRequest>(), default))
                .ReturnsAsync(completeResponse);

            // Act
            var result = await _service.StoreVideoAsync(content, metadata, progressCallbacks.Add);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("video/", result.StorageKey);
            Assert.EndsWith(".mp4", result.StorageKey);
            Assert.Equal(videoContent.Length, result.SizeBytes);
            Assert.NotEmpty(progressCallbacks);

            // Verify multipart upload was used
            _mockS3Client.Verify(x => x.InitiateMultipartUploadAsync(It.IsAny<InitiateMultipartUploadRequest>(), default), 
                Times.Once);
            _mockS3Client.Verify(x => x.UploadPartAsync(It.IsAny<UploadPartRequest>(), default), 
                Times.AtLeastOnce);
            _mockS3Client.Verify(x => x.CompleteMultipartUploadAsync(It.IsAny<CompleteMultipartUploadRequest>(), default), 
                Times.Once);
        }

        #endregion

        #region GetVideoStreamAsync Tests

        [Fact(Skip = "Requires mocking non-virtual AWS SDK properties")]
        public async Task GetVideoStreamAsync_WithFullRange_ShouldReturnFullStream()
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var videoContent = "full video content data";
            var contentLength = videoContent.Length;

            var metadataResponse = new GetObjectMetadataResponse
            {
                ContentLength = contentLength,
                HttpStatusCode = HttpStatusCode.OK
            };

            var getObjectResponse = new GetObjectResponse
            {
                ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(videoContent)),
                HttpStatusCode = HttpStatusCode.OK
            };

            // Mock the Headers property with a dictionary that has ContentType
            var mockGetObjectResponse = new Mock<GetObjectResponse>();
            mockGetObjectResponse.Setup(r => r.ResponseStream).Returns(getObjectResponse.ResponseStream);
            mockGetObjectResponse.Setup(r => r.Headers.ContentType).Returns("video/mp4");

            _mockS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
                .ReturnsAsync(metadataResponse);

            _mockS3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
                .ReturnsAsync(mockGetObjectResponse.Object);

            // Act
            var rangedStream = await _service.GetVideoStreamAsync(storageKey);

            // Assert
            Assert.NotNull(rangedStream);
            Assert.Equal(0, rangedStream.RangeStart);
            Assert.Equal(contentLength - 1, rangedStream.RangeEnd);
            Assert.Equal(contentLength, rangedStream.TotalSize);
            Assert.Equal("video/mp4", rangedStream.ContentType);

            // Verify no byte range was set (full file)
            _mockS3Client.Verify(x => x.GetObjectAsync(It.Is<GetObjectRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == storageKey &&
                req.ByteRange == null
            ), default), Times.Once);
        }

        [Fact(Skip = "Requires mocking non-virtual AWS SDK properties")]
        public async Task GetVideoStreamAsync_WithRangeRequest_ShouldReturnRangedStream()
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var contentLength = 1000;
            var rangeStart = 100L;
            var rangeEnd = 200L;

            var metadataResponse = new GetObjectMetadataResponse
            {
                ContentLength = contentLength,
                HttpStatusCode = HttpStatusCode.OK
            };

            var getObjectResponse = new GetObjectResponse
            {
                ResponseStream = new MemoryStream(new byte[rangeEnd - rangeStart + 1]),
                HttpStatusCode = HttpStatusCode.PartialContent
            };

            var mockGetObjectResponse = new Mock<GetObjectResponse>();
            mockGetObjectResponse.Setup(r => r.ResponseStream).Returns(getObjectResponse.ResponseStream);
            mockGetObjectResponse.Setup(r => r.Headers.ContentType).Returns("video/mp4");

            _mockS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
                .ReturnsAsync(metadataResponse);

            _mockS3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
                .ReturnsAsync(mockGetObjectResponse.Object);

            // Act
            var rangedStream = await _service.GetVideoStreamAsync(storageKey, rangeStart, rangeEnd);

            // Assert
            Assert.NotNull(rangedStream);
            Assert.Equal(rangeStart, rangedStream.RangeStart);
            Assert.Equal(rangeEnd, rangedStream.RangeEnd);
            Assert.Equal(contentLength, rangedStream.TotalSize);

            // Verify byte range was set
            _mockS3Client.Verify(x => x.GetObjectAsync(It.Is<GetObjectRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == storageKey &&
                req.ByteRange != null
            ), default), Times.Once);
        }

        [Fact]
        public async Task GetVideoStreamAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Arrange
            var storageKey = "non-existent-key";

            _mockS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
                .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            // Act
            var rangedStream = await _service.GetVideoStreamAsync(storageKey);

            // Assert
            Assert.Null(rangedStream);
        }

        #endregion
    }
}