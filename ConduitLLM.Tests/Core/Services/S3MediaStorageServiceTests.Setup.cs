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
    }
}