using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using ItExpr = Moq.Protected.ItExpr;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for FileRetrievalService covering file retrieval logic, caching behavior, and error handling.
    /// </summary>
    public class FileRetrievalServiceTests : IDisposable
    {
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<FileRetrievalService>> _mockLogger;
        private readonly HttpClient _httpClient;
        private readonly FileRetrievalService _service;

        public FileRetrievalServiceTests()
        {
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<FileRetrievalService>>();

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

            _service = new FileRetrievalService(
                _mockStorageService.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task RetrieveFileAsync_WithStorageKey_RetrievesFromStorage()
        {
            // Arrange
            var storageKey = "image/test-hash.jpg";
            var fileData = Encoding.UTF8.GetBytes("test-image-data");
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "image/jpeg",
                SizeBytes = fileData.Length,
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CreatedAt = DateTime.UtcNow,
                CustomMetadata = new Dictionary<string, string> { ["tag"] = "test" }
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetStreamAsync(storageKey))
                .ReturnsAsync(new MemoryStream(fileData));

            // Act
            var result = await _service.RetrieveFileAsync(storageKey);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ContentStream);
            Assert.NotNull(result.Metadata);
            Assert.Equal("test.jpg", result.Metadata.FileName);
            Assert.Equal("image/jpeg", result.Metadata.ContentType);
            Assert.Equal(fileData.Length, result.Metadata.SizeBytes);
            Assert.Equal("storage", result.Metadata.StorageProvider);
            Assert.Equal($"\"{storageKey}\"", result.Metadata.ETag);
            Assert.Equal("test", result.Metadata.AdditionalMetadata["tag"]);

            using (result)
            {
                var retrievedData = new byte[fileData.Length];
                await result.ContentStream.ReadExactlyAsync(retrievedData, 0, retrievedData.Length);
                Assert.Equal(fileData, retrievedData);
            }

            _mockStorageService.Verify(x => x.GetInfoAsync(storageKey), Times.Once);
            _mockStorageService.Verify(x => x.GetStreamAsync(storageKey), Times.Once);
        }

        [Fact]
        public async Task RetrieveFileAsync_WithNonExistentStorageKey_ReturnsNull()
        {
            // Arrange
            var storageKey = "non-existent-key";

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync((MediaInfo?)null);

            // Act
            var result = await _service.RetrieveFileAsync(storageKey);

            // Assert
            Assert.Null(result);
            _mockStorageService.Verify(x => x.GetInfoAsync(storageKey), Times.Once);
            _mockStorageService.Verify(x => x.GetStreamAsync(storageKey), Times.Never);
        }

        [Fact]
        public async Task RetrieveFileAsync_WithStorageKeyButNoStream_ReturnsNull()
        {
            // Arrange
            var storageKey = "image/test-hash.jpg";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "image/jpeg",
                SizeBytes = 1000,
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetStreamAsync(storageKey))
                .ReturnsAsync((Stream?)null);

            // Act
            var result = await _service.RetrieveFileAsync(storageKey);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RetrieveFileAsync_WithHttpUrl_RetrievesFromUrl()
        {
            // Arrange
            var url = "https://example.com/test-image.jpg";
            var fileData = Encoding.UTF8.GetBytes("remote-image-data");
            var lastModified = DateTimeOffset.UtcNow.AddHours(-1);
            var etag = "\"test-etag\"";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(fileData)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentLength = fileData.Length;
            response.Content.Headers.LastModified = lastModified;
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "\"remote-image.jpg\""
            };
            response.Headers.Add("Accept-Ranges", "bytes");

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _service.RetrieveFileAsync(url);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ContentStream);
            Assert.NotNull(result.Metadata);
            Assert.Equal("remote-image.jpg", result.Metadata.FileName);
            Assert.Equal("image/jpeg", result.Metadata.ContentType);
            Assert.Equal(fileData.Length, result.Metadata.SizeBytes);
            Assert.Equal("url", result.Metadata.StorageProvider);
            Assert.Equal(etag, result.Metadata.ETag);
            Assert.Equal(lastModified.DateTime, result.Metadata.ModifiedAt);
            Assert.True(result.Metadata.SupportsRangeRequests);

            using (result)
            {
                var retrievedData = new byte[fileData.Length];
                await result.ContentStream.ReadExactlyAsync(retrievedData, 0, retrievedData.Length);
                Assert.Equal(fileData, retrievedData);
            }
        }

        [Fact]
        public async Task RetrieveFileAsync_WithHttpUrlNoContentDisposition_ExtractsFilenameFromUrl()
        {
            // Arrange
            var url = "https://example.com/path/to/image.png";
            var fileData = Encoding.UTF8.GetBytes("image-data");

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(fileData)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            response.Content.Headers.ContentLength = fileData.Length;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _service.RetrieveFileAsync(url);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("image.png", result.Metadata.FileName);
        }

        [Fact]
        public async Task RetrieveFileAsync_WithHttpUrlNotFound_ReturnsNull()
        {
            // Arrange
            var url = "https://example.com/not-found.jpg";

            var response = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _service.RetrieveFileAsync(url);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RetrieveFileAsync_WithNullOrEmptyIdentifier_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(await _service.RetrieveFileAsync(null));
            Assert.Null(await _service.RetrieveFileAsync(""));
            Assert.Null(await _service.RetrieveFileAsync("   "));
        }

        [Fact]
        public async Task RetrieveFileAsync_WithException_ReturnsNull()
        {
            // Arrange
            var storageKey = "error-key";

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ThrowsAsync(new InvalidOperationException("Storage error"));

            // Act
            var result = await _service.RetrieveFileAsync(storageKey);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadFileAsync_WithValidFile_DownloadsSuccessfully()
        {
            // Arrange
            var storageKey = "test-file";
            var fileData = Encoding.UTF8.GetBytes("test-file-data");
            var destinationPath = Path.GetTempFileName();

            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "text/plain",
                SizeBytes = fileData.Length,
                FileName = "test.txt",
                MediaType = MediaType.Other,
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetStreamAsync(storageKey))
                .ReturnsAsync(new MemoryStream(fileData));

            try
            {
                // Act
                var result = await _service.DownloadFileAsync(storageKey, destinationPath);

                // Assert
                Assert.True(result);
                Assert.True(File.Exists(destinationPath));

                var downloadedData = await File.ReadAllBytesAsync(destinationPath);
                Assert.Equal(fileData, downloadedData);
            }
            finally
            {
                // Cleanup
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
            }
        }

        [Fact]
        public async Task DownloadFileAsync_WithNonExistentFile_ReturnsFalse()
        {
            // Arrange
            var storageKey = "non-existent-file";
            var destinationPath = Path.GetTempFileName();

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync((MediaInfo?)null);

            try
            {
                // Act
                var result = await _service.DownloadFileAsync(storageKey, destinationPath);

                // Assert
                Assert.False(result);
            }
            finally
            {
                // Cleanup
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
            }
        }

        [Fact]
        public async Task DownloadFileAsync_CreatesDirectoryIfNotExists()
        {
            // Arrange
            var storageKey = "test-file";
            var fileData = Encoding.UTF8.GetBytes("test-data");
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var destinationPath = Path.Combine(tempDir, "subdir", "test.txt");

            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "text/plain",
                SizeBytes = fileData.Length,
                FileName = "test.txt",
                MediaType = MediaType.Other,
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetStreamAsync(storageKey))
                .ReturnsAsync(new MemoryStream(fileData));

            try
            {
                // Act
                var result = await _service.DownloadFileAsync(storageKey, destinationPath);

                // Assert
                Assert.True(result);
                Assert.True(Directory.Exists(Path.GetDirectoryName(destinationPath)));
                Assert.True(File.Exists(destinationPath));

                var downloadedData = await File.ReadAllBytesAsync(destinationPath);
                Assert.Equal(fileData, downloadedData);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task GetDownloadUrlAsync_WithStorageKey_GeneratesUrl()
        {
            // Arrange
            var storageKey = "test-key";
            var expiration = TimeSpan.FromHours(2);
            var expectedUrl = "https://storage.example.com/test-key";

            _mockStorageService.Setup(x => x.GenerateUrlAsync(storageKey, expiration))
                .ReturnsAsync(expectedUrl);

            // Act
            var url = await _service.GetDownloadUrlAsync(storageKey, expiration);

            // Assert
            Assert.Equal(expectedUrl, url);
            _mockStorageService.Verify(x => x.GenerateUrlAsync(storageKey, expiration), Times.Once);
        }

        [Fact]
        public async Task GetDownloadUrlAsync_WithHttpUrl_ReturnsOriginalUrl()
        {
            // Arrange
            var httpUrl = "https://example.com/file.jpg";
            var expiration = TimeSpan.FromHours(1);

            // Act
            var url = await _service.GetDownloadUrlAsync(httpUrl, expiration);

            // Assert
            Assert.Equal(httpUrl, url);
            _mockStorageService.Verify(x => x.GenerateUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public async Task GetDownloadUrlAsync_WithNullOrEmptyIdentifier_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(await _service.GetDownloadUrlAsync(null, TimeSpan.FromHours(1)));
            Assert.Null(await _service.GetDownloadUrlAsync("", TimeSpan.FromHours(1)));
            Assert.Null(await _service.GetDownloadUrlAsync("   ", TimeSpan.FromHours(1)));
        }

        [Fact]
        public async Task GetFileMetadataAsync_WithStorageKey_ReturnsMetadata()
        {
            // Arrange
            var storageKey = "test-key";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "image/jpeg",
                SizeBytes = 1024,
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CreatedAt = DateTime.UtcNow,
                CustomMetadata = new Dictionary<string, string> { ["purpose"] = "testing" }
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            // Act
            var metadata = await _service.GetFileMetadataAsync(storageKey);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("test.jpg", metadata.FileName);
            Assert.Equal("image/jpeg", metadata.ContentType);
            Assert.Equal(1024, metadata.SizeBytes);
            Assert.Equal(mediaInfo.CreatedAt, metadata.CreatedAt);
            Assert.Equal(mediaInfo.CreatedAt, metadata.ModifiedAt);
            Assert.Equal("storage", metadata.StorageProvider);
            Assert.Equal($"\"{storageKey}\"", metadata.ETag);
            Assert.Equal("testing", metadata.AdditionalMetadata["purpose"]);
        }

        [Fact]
        public async Task GetFileMetadataAsync_WithHttpUrl_ReturnsUrlMetadata()
        {
            // Arrange
            var url = "https://example.com/test.jpg";
            var lastModified = DateTimeOffset.UtcNow.AddHours(-2);
            var etag = "\"url-etag\"";

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            response.Content.Headers.ContentLength = 2048;
            response.Content.Headers.LastModified = lastModified;
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("inline")
            {
                FileName = "\"remote.jpg\""
            };
            response.Headers.Add("Accept-Ranges", "bytes");

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Head && 
                        req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var metadata = await _service.GetFileMetadataAsync(url);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("remote.jpg", metadata.FileName);
            Assert.Equal("image/jpeg", metadata.ContentType);
            Assert.Equal(2048, metadata.SizeBytes);
            Assert.Equal(lastModified.DateTime, metadata.ModifiedAt);
            Assert.Equal("url", metadata.StorageProvider);
            Assert.Equal(etag, metadata.ETag);
            Assert.True(metadata.SupportsRangeRequests);
        }

        [Fact]
        public async Task GetFileMetadataAsync_WithHttpUrlNotFound_ReturnsNull()
        {
            // Arrange
            var url = "https://example.com/not-found.jpg";

            var response = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Head && 
                        req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var metadata = await _service.GetFileMetadataAsync(url);

            // Assert
            Assert.Null(metadata);
        }

        [Fact]
        public async Task FileExistsAsync_WithExistingStorageKey_ReturnsTrue()
        {
            // Arrange
            var storageKey = "existing-key";

            _mockStorageService.Setup(x => x.ExistsAsync(storageKey))
                .ReturnsAsync(true);

            // Act
            var exists = await _service.FileExistsAsync(storageKey);

            // Assert
            Assert.True(exists);
            _mockStorageService.Verify(x => x.ExistsAsync(storageKey), Times.Once);
        }

        [Fact]
        public async Task FileExistsAsync_WithNonExistentStorageKey_ReturnsFalse()
        {
            // Arrange
            var storageKey = "non-existent-key";

            _mockStorageService.Setup(x => x.ExistsAsync(storageKey))
                .ReturnsAsync(false);

            // Act
            var exists = await _service.FileExistsAsync(storageKey);

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task FileExistsAsync_WithExistingHttpUrl_ReturnsTrue()
        {
            // Arrange
            var url = "https://example.com/existing.jpg";

            var response = new HttpResponseMessage(HttpStatusCode.OK);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Head && 
                        req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var exists = await _service.FileExistsAsync(url);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task FileExistsAsync_WithNonExistentHttpUrl_ReturnsFalse()
        {
            // Arrange
            var url = "https://example.com/not-found.jpg";

            var response = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Head && 
                        req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var exists = await _service.FileExistsAsync(url);

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task FileExistsAsync_WithHttpException_ReturnsFalse()
        {
            // Arrange
            var url = "https://example.com/error.jpg";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Head && 
                        req.RequestUri.ToString() == url),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var exists = await _service.FileExistsAsync(url);

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task FileExistsAsync_WithNullOrEmptyIdentifier_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(await _service.FileExistsAsync(null));
            Assert.False(await _service.FileExistsAsync(""));
            Assert.False(await _service.FileExistsAsync("   "));
        }

        [Theory]
        [InlineData("https://example.com/file.jpg")]
        [InlineData("http://example.com/file.jpg")]
        [InlineData("HTTP://EXAMPLE.COM/FILE.JPG")]
        [InlineData("HTTPS://EXAMPLE.COM/FILE.JPG")]
        public async Task IsUrl_DetectsHttpUrls(string url)
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _service.RetrieveFileAsync(url);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("url", result.Metadata.StorageProvider);
        }

        [Theory]
        [InlineData("storage-key")]
        [InlineData("image/test-hash.jpg")]
        [InlineData("ftp://example.com/file.jpg")]
        [InlineData("file:///local/path/file.jpg")]
        public async Task IsUrl_DoesNotDetectNonHttpUrls(string identifier)
        {
            // Arrange
            _mockStorageService.Setup(x => x.GetInfoAsync(identifier))
                .ReturnsAsync((MediaInfo?)null);

            // Act
            var result = await _service.RetrieveFileAsync(identifier);

            // Assert
            Assert.Null(result); // Should try storage service, not HTTP
            _mockStorageService.Verify(x => x.GetInfoAsync(identifier), Times.Once);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}