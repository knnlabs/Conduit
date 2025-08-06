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
                Region = "us-east-1", // Add default region to prevent AWS client error
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
                Region = "us-east-1", // Add default region to prevent AWS client error
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
    }
}