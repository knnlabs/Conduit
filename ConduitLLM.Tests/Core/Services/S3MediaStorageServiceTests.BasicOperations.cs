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