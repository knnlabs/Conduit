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
    public class S3MediaStorageServiceTests
    {
        private readonly Mock<IAmazonS3> _mockS3Client;
        private readonly Mock<ILogger<S3MediaStorageService>> _mockLogger;
        private readonly Mock<IOptions<S3StorageOptions>> _mockOptions;
        private readonly S3StorageOptions _options;
        private readonly S3MediaStorageService _service;

        public S3MediaStorageServiceTests()
        {
            _mockS3Client = new Mock<IAmazonS3>();
            _mockLogger = new Mock<ILogger<S3MediaStorageService>>();
            _mockOptions = new Mock<IOptions<S3StorageOptions>>();
            
            _options = new S3StorageOptions
            {
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = "test-bucket",
                Region = "us-east-1",
                ServiceUrl = "https://s3.amazonaws.com",
                ForcePathStyle = true,
                AutoCreateBucket = true,
                DefaultUrlExpiration = TimeSpan.FromHours(1),
                MaxFileSizeBytes = 100 * 1024 * 1024, // 100MB
                PublicBaseUrl = "https://cdn.example.com"
            };

            _mockOptions.Setup(x => x.Value).Returns(_options);

            // Setup bucket head request to succeed (bucket exists)
            _mockS3Client.Setup(x => x.HeadBucketAsync(It.IsAny<HeadBucketRequest>(), default))
                .ReturnsAsync(new HeadBucketResponse());

            // Create service with mocked dependencies
            _service = CreateServiceWithMockedS3Client();
        }

        private S3MediaStorageService CreateServiceWithMockedS3Client()
        {
            // We need to use reflection to inject the mocked S3 client
            var service = new S3MediaStorageService(_mockOptions.Object, _mockLogger.Object);
            
            // Use reflection to replace the S3 client
            var s3ClientField = typeof(S3MediaStorageService).GetField("_s3Client", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            s3ClientField?.SetValue(service, _mockS3Client.Object);
            
            // Replace the TransferUtility with a null to prevent real AWS calls
            var transferUtilityField = typeof(S3MediaStorageService).GetField("_transferUtility",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            transferUtilityField?.SetValue(service, null);
            
            return service;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithMissingAccessKey_ShouldThrowException()
        {
            // Arrange
            var options = new S3StorageOptions
            {
                AccessKey = "",
                SecretKey = "test-secret",
                BucketName = "test-bucket"
            };
            var mockOptions = new Mock<IOptions<S3StorageOptions>>();
            mockOptions.Setup(x => x.Value).Returns(options);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                new S3MediaStorageService(mockOptions.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithMissingSecretKey_ShouldThrowException()
        {
            // Arrange
            var options = new S3StorageOptions
            {
                AccessKey = "test-access",
                SecretKey = "",
                BucketName = "test-bucket"
            };
            var mockOptions = new Mock<IOptions<S3StorageOptions>>();
            mockOptions.Setup(x => x.Value).Returns(options);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                new S3MediaStorageService(mockOptions.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithMissingBucketName_ShouldThrowException()
        {
            // Arrange
            var options = new S3StorageOptions
            {
                AccessKey = "test-access",
                SecretKey = "test-secret",
                BucketName = ""
            };
            var mockOptions = new Mock<IOptions<S3StorageOptions>>();
            mockOptions.Setup(x => x.Value).Returns(options);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                new S3MediaStorageService(mockOptions.Object, _mockLogger.Object));
        }

        #endregion

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
                CreatedBy = "test-user",
                CustomMetadata = new Dictionary<string, string> { ["source"] = "test" }
            };

            var putObjectResponse = new PutObjectResponse
            {
                ETag = "test-etag",
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                .ReturnsAsync(putObjectResponse);

            _mockS3Client.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                .ReturnsAsync("https://example.com/signed-url");

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

            // Verify S3 calls
            _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_WithPublicBaseUrl_ShouldUsePublicUrl()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/png",
                FileName = "test.png",
                MediaType = MediaType.Image
            };

            var putObjectResponse = new PutObjectResponse
            {
                ETag = "test-etag",
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                .ReturnsAsync(putObjectResponse);

            // Act
            var result = await _service.StoreAsync(content, metadata);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith(_options.PublicBaseUrl, result.Url);
            
            // Should not call GetPreSignedURLAsync when PublicBaseUrl is set
            _mockS3Client.Verify(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()), 
                Times.Never);
        }

        [Fact]
        public async Task StoreAsync_WithExpirationMetadata_ShouldIncludeExpiration()
        {
            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test data"));
            var expiresAt = DateTime.UtcNow.AddHours(24);
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                ExpiresAt = expiresAt
            };

            var putObjectResponse = new PutObjectResponse
            {
                ETag = "test-etag",
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                .ReturnsAsync(putObjectResponse);

            // Act
            var result = await _service.StoreAsync(content, metadata);

            // Assert
            Assert.NotNull(result);
            
            // Verify put object was called
            _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
        }

        #endregion

        #region GetStreamAsync Tests

        [Fact]
        public async Task GetStreamAsync_WithExistingKey_ShouldReturnStream()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var testData = "test image data";
            var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
            
            var getObjectResponse = new GetObjectResponse
            {
                ResponseStream = responseStream,
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.GetObjectAsync(It.Is<GetObjectRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == storageKey
            ), default)).ReturnsAsync(getObjectResponse);

            // Act
            var stream = await _service.GetStreamAsync(storageKey);

            // Assert
            Assert.NotNull(stream);
            Assert.Same(responseStream, stream);
        }

        [Fact]
        public async Task GetStreamAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Arrange
            var storageKey = "non-existent-key";
            
            _mockS3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
                .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            // Act
            var stream = await _service.GetStreamAsync(storageKey);

            // Assert
            Assert.Null(stream);
        }

        [Fact]
        public async Task GetStreamAsync_WithS3Exception_ShouldThrowException()
        {
            // Arrange
            var storageKey = "test-key";
            
            _mockS3Client.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
                .ThrowsAsync(new AmazonS3Exception("Access Denied") { StatusCode = HttpStatusCode.Forbidden });

            // Act & Assert
            await Assert.ThrowsAsync<AmazonS3Exception>(() => _service.GetStreamAsync(storageKey));
        }

        #endregion

        #region GetInfoAsync Tests

        [Fact]
        public async Task GetInfoAsync_WithExistingKey_ShouldReturnMediaInfo()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var lastModified = DateTime.UtcNow.AddHours(-1);
            
            var metadataResponse = new GetObjectMetadataResponse
            {
                ContentLength = 1024,
                LastModified = lastModified
            };

            // Configure the response headers
            metadataResponse.Headers.ContentType = "image/jpeg";
            
            // Add metadata fields - ensure the Metadata collection is properly initialized
            metadataResponse.Metadata.Add("content-type", "image/jpeg");
            metadataResponse.Metadata.Add("original-filename", "test.jpg");
            metadataResponse.Metadata.Add("media-type", "Image");
            metadataResponse.Metadata.Add("custom-source", "test-source");

            _mockS3Client.Setup(x => x.GetObjectMetadataAsync(It.Is<GetObjectMetadataRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == storageKey
            ), default)).ReturnsAsync(metadataResponse);

            // Act
            var info = await _service.GetInfoAsync(storageKey);

            // Assert
            Assert.NotNull(info);
            Assert.Equal(storageKey, info.StorageKey);
            Assert.Equal(1024, info.SizeBytes);
            Assert.Equal(MediaType.Image, info.MediaType);
            Assert.Equal("test.jpg", info.FileName);
            Assert.Equal(lastModified, info.CreatedAt);
            Assert.Contains("source", info.CustomMetadata.Keys);
            Assert.Equal("test-source", info.CustomMetadata["source"]);
        }

        [Fact]
        public async Task GetInfoAsync_WithNonExistentKey_ShouldReturnNull()
        {
            // Arrange
            var storageKey = "non-existent-key";
            
            _mockS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
                .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            // Act
            var info = await _service.GetInfoAsync(storageKey);

            // Assert
            Assert.Null(info);
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_WithExistingKey_ShouldDeleteAndReturnTrue()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            
            var deleteResponse = new DeleteObjectResponse
            {
                HttpStatusCode = HttpStatusCode.NoContent
            };

            _mockS3Client.Setup(x => x.DeleteObjectAsync(It.Is<DeleteObjectRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == storageKey
            ), default)).ReturnsAsync(deleteResponse);

            // Act
            var result = await _service.DeleteAsync(storageKey);

            // Assert
            Assert.True(result);
            _mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default), 
                Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WithException_ShouldReturnFalse()
        {
            // Arrange
            var storageKey = "test-key";
            
            _mockS3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
                .ThrowsAsync(new AmazonS3Exception("Access Denied"));

            // Act
            var result = await _service.DeleteAsync(storageKey);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GenerateUrlAsync Tests

        [Fact]
        public async Task GenerateUrlAsync_WithPublicBaseUrl_ShouldReturnPublicUrl()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";

            // Act
            var url = await _service.GenerateUrlAsync(storageKey);

            // Assert
            Assert.Equal($"{_options.PublicBaseUrl}/{storageKey}", url);
            
            // Should not call GetPreSignedURLAsync when PublicBaseUrl is set
            _mockS3Client.Verify(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()), 
                Times.Never);
        }

        [Fact]
        public async Task GenerateUrlAsync_WithoutPublicBaseUrl_ShouldReturnPresignedUrl()
        {
            // Arrange
            var optionsWithoutPublicUrl = new S3StorageOptions
            {
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = "test-bucket",
                DefaultUrlExpiration = TimeSpan.FromHours(1)
            };

            var mockOptionsWithoutPublicUrl = new Mock<IOptions<S3StorageOptions>>();
            mockOptionsWithoutPublicUrl.Setup(x => x.Value).Returns(optionsWithoutPublicUrl);

            var serviceWithoutPublicUrl = new S3MediaStorageService(mockOptionsWithoutPublicUrl.Object, _mockLogger.Object);
            var s3ClientField = typeof(S3MediaStorageService).GetField("_s3Client", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            s3ClientField?.SetValue(serviceWithoutPublicUrl, _mockS3Client.Object);

            var storageKey = "image/2023/01/01/test-hash.jpg";
            var presignedUrl = "https://s3.amazonaws.com/test-bucket/signed-url";

            _mockS3Client.Setup(x => x.GetPreSignedURLAsync(It.Is<GetPreSignedUrlRequest>(req =>
                req.BucketName == optionsWithoutPublicUrl.BucketName &&
                req.Key == storageKey &&
                req.Verb == HttpVerb.GET
            ))).ReturnsAsync(presignedUrl);

            // Act
            var url = await serviceWithoutPublicUrl.GenerateUrlAsync(storageKey);

            // Assert
            Assert.Equal(presignedUrl, url);
            _mockS3Client.Verify(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()), 
                Times.Once);
        }

        [Fact]
        public async Task GenerateUrlAsync_WithCustomExpiration_ShouldUseCustomExpiration()
        {
            // Arrange
            var optionsWithoutPublicUrl = new S3StorageOptions
            {
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = "test-bucket",
                DefaultUrlExpiration = TimeSpan.FromHours(1)
            };

            var mockOptionsWithoutPublicUrl = new Mock<IOptions<S3StorageOptions>>();
            mockOptionsWithoutPublicUrl.Setup(x => x.Value).Returns(optionsWithoutPublicUrl);

            var serviceWithoutPublicUrl = new S3MediaStorageService(mockOptionsWithoutPublicUrl.Object, _mockLogger.Object);
            var s3ClientField = typeof(S3MediaStorageService).GetField("_s3Client", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            s3ClientField?.SetValue(serviceWithoutPublicUrl, _mockS3Client.Object);

            var storageKey = "test-key";
            var customExpiration = TimeSpan.FromHours(2);
            var presignedUrl = "https://s3.amazonaws.com/test-bucket/signed-url";

            _mockS3Client.Setup(x => x.GetPreSignedURLAsync(It.Is<GetPreSignedUrlRequest>(req =>
                req.Expires > DateTime.UtcNow.Add(TimeSpan.FromMinutes(110)) && // Should be close to 2 hours
                req.Expires < DateTime.UtcNow.Add(TimeSpan.FromMinutes(130))
            ))).ReturnsAsync(presignedUrl);

            // Act
            var url = await serviceWithoutPublicUrl.GenerateUrlAsync(storageKey, customExpiration);

            // Assert
            Assert.Equal(presignedUrl, url);
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WithExistingKey_ShouldReturnTrue()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            
            var metadataResponse = new GetObjectMetadataResponse
            {
                ContentLength = 1024,
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.GetObjectMetadataAsync(It.Is<GetObjectMetadataRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Key == storageKey
            ), default)).ReturnsAsync(metadataResponse);

            // Act
            var exists = await _service.ExistsAsync(storageKey);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistentKey_ShouldReturnFalse()
        {
            // Arrange
            var storageKey = "non-existent-key";
            
            _mockS3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
                .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            // Act
            var exists = await _service.ExistsAsync(storageKey);

            // Assert
            Assert.False(exists);
        }

        #endregion

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
                req.ContentType == "video/mp4" &&
                req.ServerSideEncryptionMethod == ServerSideEncryptionMethod.AES256
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

        #region GeneratePresignedUploadUrlAsync Tests

        [Fact]
        public async Task GeneratePresignedUploadUrlAsync_ShouldReturnValidUrl()
        {
            // Arrange
            var metadata = new VideoMediaMetadata
            {
                ContentType = "video/mp4",
                FileName = "upload.mp4",
                Duration = 120,
                Resolution = "1920x1080",
                GeneratedByModel = "test-model"
            };
            var expiration = TimeSpan.FromHours(1);
            var presignedUrl = "https://s3.amazonaws.com/test-bucket/presigned-upload-url";

            _mockS3Client.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
                .ReturnsAsync(presignedUrl);

            // Act
            var result = await _service.GeneratePresignedUploadUrlAsync(metadata, expiration);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(presignedUrl, result.Url);
            Assert.Equal("PUT", result.HttpMethod);
            Assert.Contains("Content-Type", result.RequiredHeaders.Keys);
            Assert.Equal("video/mp4", result.RequiredHeaders["Content-Type"]);
            Assert.Contains("x-amz-server-side-encryption", result.RequiredHeaders.Keys);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
            Assert.Equal(5L * 1024 * 1024 * 1024, result.MaxFileSizeBytes); // 5GB

            // Verify presigned URL request
            _mockS3Client.Verify(x => x.GetPreSignedURLAsync(It.Is<GetPreSignedUrlRequest>(req =>
                req.BucketName == _options.BucketName &&
                req.Verb == HttpVerb.PUT &&
                req.ContentType == "video/mp4" &&
                req.Protocol == Protocol.HTTPS &&
                req.Headers.Keys.Contains("x-amz-meta-generated-by-model") &&
                req.Headers["x-amz-meta-generated-by-model"] == "test-model"
            )), Times.Once);
        }

        #endregion

        #region Private Method Tests

        [Fact]
        public async Task StorageKeyGeneration_ShouldIncludeDateFolder()
        {
            // This test verifies the storage key generation includes date folders
            // We can't directly test the private method, but we can verify the behavior
            // through the public StoreAsync method

            // Arrange
            var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
            var metadata = new MediaMetadata
            {
                ContentType = "image/jpeg",
                FileName = "test.jpg",
                MediaType = MediaType.Image
            };

            var putObjectResponse = new PutObjectResponse
            {
                ETag = "test-etag",
                HttpStatusCode = HttpStatusCode.OK
            };

            _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                .ReturnsAsync(putObjectResponse);

            // Act
            var result = await _service.StoreAsync(content, metadata);

            // Assert
            Assert.NotNull(result.StorageKey);
            Assert.StartsWith("image/", result.StorageKey);
            
            // Verify the storage key includes date folder structure (yyyy/MM/dd)
            var keyParts = result.StorageKey.Split('/');
            Assert.True(keyParts.Length >= 4); // type/year/month/day/filename
            
            // Verify year is current year
            Assert.True(int.TryParse(keyParts[1], out var year));
            Assert.True(year >= 2023 && year <= 2030);
            
            // Verify month is valid
            Assert.True(int.TryParse(keyParts[2], out var month));
            Assert.True(month >= 1 && month <= 12);
            
            // Verify day is valid
            Assert.True(int.TryParse(keyParts[3], out var day));
            Assert.True(day >= 1 && day <= 31);
        }

        #endregion
    }
}