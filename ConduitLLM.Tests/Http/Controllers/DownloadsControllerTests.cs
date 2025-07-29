using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class DownloadsControllerTests : ControllerTestBase
    {
        private readonly Mock<IFileRetrievalService> _mockFileRetrievalService;
        private readonly Mock<ILogger<DownloadsController>> _mockLogger;
        private readonly Mock<IMediaRecordRepository> _mockMediaRecordRepository;
        private readonly DownloadsController _controller;

        public DownloadsControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockFileRetrievalService = new Mock<IFileRetrievalService>();
            _mockLogger = CreateLogger<DownloadsController>();
            _mockMediaRecordRepository = new Mock<IMediaRecordRepository>();

            _controller = new DownloadsController(
                _mockFileRetrievalService.Object,
                _mockLogger.Object,
                _mockMediaRecordRepository.Object);

            _controller.ControllerContext = CreateControllerContext();
        }

        #region DownloadFile Tests

        [Fact]
        public async Task DownloadFile_WithExistingFile_ShouldReturnFileResult()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            var contentBytes = Encoding.UTF8.GetBytes("Test file content");
            var contentStream = new MemoryStream(contentBytes);
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            var fileResult = new FileRetrievalResult
            {
                ContentStream = contentStream,
                Metadata = new FileMetadata
                {
                    FileName = "test.txt",
                    ContentType = "text/plain",
                    SizeBytes = contentBytes.Length,
                    ETag = "\"test-etag\"",
                    SupportsRangeRequests = true
                }
            };

            _mockFileRetrievalService.Setup(x => x.RetrieveFileAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileResult);

            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            var fileActionResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("text/plain", fileActionResult.ContentType);
            Assert.Equal("test.txt", fileActionResult.FileDownloadName);
            Assert.True(fileActionResult.EnableRangeProcessing);
        }

        [Fact]
        public async Task DownloadFile_WithInlineTrue_ShouldNotSetContentDisposition()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            var contentStream = new MemoryStream(new byte[] { 1, 2, 3 });
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            var fileResult = new FileRetrievalResult
            {
                ContentStream = contentStream,
                Metadata = new FileMetadata
                {
                    FileName = "image.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = 3
                }
            };

            _mockFileRetrievalService.Setup(x => x.RetrieveFileAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileResult);

            // Act
            var result = await _controller.DownloadFile(fileId, inline: true);

            // Assert
            Assert.IsType<FileStreamResult>(result);
            Assert.False(_controller.Response.Headers.ContainsKey("Content-Disposition"));
        }

        [Fact]
        public async Task DownloadFile_WithETag_ShouldSetCacheHeaders()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            var contentStream = new MemoryStream();
            var etag = "\"12345\"";
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            var fileResult = new FileRetrievalResult
            {
                ContentStream = contentStream,
                Metadata = new FileMetadata
                {
                    ContentType = "application/octet-stream",
                    ETag = etag
                }
            };

            _mockFileRetrievalService.Setup(x => x.RetrieveFileAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileResult);

            // Act
            await _controller.DownloadFile(fileId);

            // Assert
            Assert.Equal(etag, _controller.Response.Headers["ETag"]);
            Assert.Equal("private, max-age=3600", _controller.Response.Headers["Cache-Control"]);
        }

        [Fact]
        public async Task DownloadFile_WithNonExistentFile_ShouldReturnNotFound()
        {
            // Arrange
            var fileId = "non-existent";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record as not found
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync((MediaRecord)null);
            
            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("File not found", error.error.message.ToString());
            Assert.Equal("not_found", error.error.type.ToString());
        }

        [Fact]
        public async Task DownloadFile_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            _mockFileRetrievalService.Setup(x => x.RetrieveFileAsync(fileId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred while downloading the file", error.error.message.ToString());
            Assert.Equal("server_error", error.error.type.ToString());
        }

        #endregion

        #region GetFileMetadata Tests

        [Fact]
        public async Task GetFileMetadata_WithExistingFile_ShouldReturnMetadata()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            var metadata = new FileMetadata
            {
                FileName = "document.pdf",
                ContentType = "application/pdf",
                SizeBytes = 102400,
                CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                ModifiedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                StorageProvider = "s3",
                ETag = "\"abc123\"",
                SupportsRangeRequests = true,
                AdditionalMetadata = new Dictionary<string, string>
                {
                    ["author"] = "Test Author"
                }
            };

            _mockFileRetrievalService.Setup(x => x.GetFileMetadataAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(metadata);

            // Act
            var result = await _controller.GetFileMetadata(fileId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.Equal("document.pdf", response.file_name.ToString());
            Assert.Equal("application/pdf", response.content_type.ToString());
            Assert.Equal(102400L, (long)response.size_bytes);
            Assert.Equal("s3", response.storage_provider.ToString());
            Assert.Equal("\"abc123\"", response.etag.ToString());
            Assert.True((bool)response.supports_range_requests);
        }

        [Fact]
        public async Task GetFileMetadata_WithNonExistentFile_ShouldReturnNotFound()
        {
            // Arrange
            var fileId = "non-existent";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record as not found
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync((MediaRecord)null);

            // Act
            var result = await _controller.GetFileMetadata(fileId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("File not found", error.error.message.ToString());
            Assert.Equal("not_found", error.error.type.ToString());
        }

        [Fact]
        public async Task GetFileMetadata_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            _mockFileRetrievalService.Setup(x => x.GetFileMetadataAsync(fileId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Metadata service error"));

            // Act
            var result = await _controller.GetFileMetadata(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred while retrieving file metadata", error.error.message.ToString());
        }

        #endregion

        #region GenerateDownloadUrl Tests

        [Fact]
        public async Task GenerateDownloadUrl_WithValidRequest_ShouldReturnUrl()
        {
            // Arrange
            var request = new GenerateUrlRequest
            {
                FileId = "test-file-id",
                ExpirationMinutes = 30
            };
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = request.FileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(request.FileId))
                .ReturnsAsync(mediaRecord);

            var expectedUrl = "https://example.com/download/test-file-id?token=abc123";
            _mockFileRetrievalService.Setup(x => x.GetDownloadUrlAsync(
                    request.FileId, 
                    It.Is<TimeSpan>(ts => ts.TotalMinutes == 30),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedUrl);

            // Act
            var result = await _controller.GenerateDownloadUrl(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.Equal(expectedUrl, response.url.ToString());
            Assert.Equal(30, (int)response.expiration_minutes);
            
            // Check expires_at is approximately correct (within 5 seconds to account for test execution time)
            var expiresAt = DateTime.Parse(response.expires_at.ToString());
            var expectedExpiresAt = DateTime.UtcNow.AddMinutes(30);
            var timeDifference = Math.Abs((expiresAt - expectedExpiresAt).TotalSeconds);
            Assert.True(timeDifference < 5, $"Expected expires_at to be within 5 seconds of {expectedExpiresAt:O}, but was {expiresAt:O} (difference: {timeDifference:F1} seconds)");
        }

        [Fact]
        public async Task GenerateDownloadUrl_WithDefaultExpiration_ShouldUse60Minutes()
        {
            // Arrange
            var request = new GenerateUrlRequest
            {
                FileId = "test-file-id"
            };
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = request.FileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(request.FileId))
                .ReturnsAsync(mediaRecord);

            _mockFileRetrievalService.Setup(x => x.GetDownloadUrlAsync(
                    request.FileId, 
                    It.Is<TimeSpan>(ts => ts.TotalMinutes == 60),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://example.com/download");

            // Act
            var result = await _controller.GenerateDownloadUrl(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            Assert.Equal(60, (int)response.expiration_minutes);
        }

        [Fact]
        public async Task GenerateDownloadUrl_WithEmptyFileId_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new GenerateUrlRequest
            {
                FileId = ""
            };
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Act
            var result = await _controller.GenerateDownloadUrl(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal("File ID is required", error.error.message.ToString());
            Assert.Equal("invalid_request_error", error.error.type.ToString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(10081)] // More than 1 week in minutes
        public async Task GenerateDownloadUrl_WithInvalidExpiration_ShouldReturnBadRequest(int expirationMinutes)
        {
            // Arrange
            var request = new GenerateUrlRequest
            {
                FileId = "test-file-id",
                ExpirationMinutes = expirationMinutes
            };
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;
            
            // Set up media record for ownership validation  
            var mediaRecord = new MediaRecord
            {
                StorageKey = request.FileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(request.FileId))
                .ReturnsAsync(mediaRecord);

            // Act
            var result = await _controller.GenerateDownloadUrl(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal("Expiration must be between 1 minute and 1 week", error.error.message.ToString());
        }

        [Fact]
        public async Task GenerateDownloadUrl_WithNonExistentFile_ShouldReturnNotFound()
        {
            // Arrange
            var request = new GenerateUrlRequest
            {
                FileId = "non-existent"
            };
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record as not found
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(request.FileId))
                .ReturnsAsync((MediaRecord)null);

            // Act
            var result = await _controller.GenerateDownloadUrl(request);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("File not found", error.error.message.ToString());
            Assert.Equal("not_found", error.error.type.ToString());
        }

        [Fact]
        public async Task GenerateDownloadUrl_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var request = new GenerateUrlRequest
            {
                FileId = "test-file-id"
            };
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = request.FileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(request.FileId))
                .ReturnsAsync(mediaRecord);

            _mockFileRetrievalService.Setup(x => x.GetDownloadUrlAsync(
                    It.IsAny<string>(), 
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("URL generation error"));

            // Act
            var result = await _controller.GenerateDownloadUrl(request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred while generating download URL", error.error.message.ToString());
        }

        #endregion

        #region CheckFileExists Tests

        [Fact]
        public async Task CheckFileExists_WithExistingFile_ShouldReturnOkWithHeaders()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            var metadata = new FileMetadata
            {
                ContentType = "image/png",
                SizeBytes = 2048,
                ETag = "\"xyz789\""
            };

            _mockFileRetrievalService.Setup(x => x.FileExistsAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockFileRetrievalService.Setup(x => x.GetFileMetadataAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(metadata);

            // Act
            var result = await _controller.CheckFileExists(fileId);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.Equal("image/png", _controller.Response.Headers["Content-Type"]);
            Assert.Equal("2048", _controller.Response.Headers["Content-Length"]);
            Assert.Equal("\"xyz789\"", _controller.Response.Headers["ETag"]);
        }

        [Fact]
        public async Task CheckFileExists_WithNonExistentFile_ShouldReturnNotFound()
        {
            // Arrange
            var fileId = "non-existent";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record as not found
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync((MediaRecord)null);

            // Act
            var result = await _controller.CheckFileExists(fileId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CheckFileExists_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            _mockFileRetrievalService.Setup(x => x.FileExistsAsync(fileId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Existence check error"));

            // Act
            var result = await _controller.CheckFileExists(fileId);

            // Assert
            var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        #endregion

        #region Ownership Validation Tests

        [Fact]
        public async Task DownloadFile_WithDifferentVirtualKeyId_ShouldReturnNotFound()
        {
            // Arrange
            var fileId = "test-file-id";
            var requestingKeyId = 123;
            var ownerKeyId = 456;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", requestingKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record with different owner
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = ownerKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("File not found", error.error.message.ToString());
            Assert.Equal("not_found", error.error.type.ToString());
        }

        [Fact]
        public async Task DownloadFile_WithUrlBasedFileId_ShouldReturnNotFound()
        {
            // Arrange
            var fileId = "https://example.com/file.pdf";
            var virtualKeyId = 123;
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;
            
            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("File not found", error.error.message.ToString());
            Assert.Equal("not_found", error.error.type.ToString());
        }

        [Fact]
        public async Task DownloadFile_WithNoVirtualKeyId_ShouldReturnNotFound()
        {
            // Arrange
            var fileId = "test-file-id";
            
            // Set up controller context without Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>();
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;
            
            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("File not found", error.error.message.ToString());
            Assert.Equal("not_found", error.error.type.ToString());
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFileRetrievalService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DownloadsController(
                null,
                _mockLogger.Object,
                _mockMediaRecordRepository.Object));
            Assert.Equal("fileRetrievalService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DownloadsController(
                _mockFileRetrievalService.Object,
                null,
                _mockMediaRecordRepository.Object));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullMediaRecordRepository_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new DownloadsController(
                _mockFileRetrievalService.Object,
                _mockLogger.Object,
                null));
            Assert.Equal("mediaRecordRepository", ex.ParamName);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(DownloadsController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task DownloadFile_WithSpecialCharactersInFileId_ShouldHandleCorrectly()
        {
            // Arrange
            var fileId = "path/to/file with spaces.txt";
            var virtualKeyId = 123;
            var contentStream = new MemoryStream();
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            var fileResult = new FileRetrievalResult
            {
                ContentStream = contentStream,
                Metadata = new FileMetadata
                {
                    ContentType = "text/plain"
                }
            };

            _mockFileRetrievalService.Setup(x => x.RetrieveFileAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileResult);

            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            Assert.IsType<FileStreamResult>(result);
            _mockFileRetrievalService.Verify(x => x.RetrieveFileAsync(fileId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DownloadFile_WithNullMetadataFields_ShouldHandleGracefully()
        {
            // Arrange
            var fileId = "test-file-id";
            var virtualKeyId = 123;
            var contentStream = new MemoryStream();
            
            // Set up controller context with Virtual Key claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("VirtualKeyId", virtualKeyId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Set up media record for ownership validation
            var mediaRecord = new MediaRecord
            {
                StorageKey = fileId,
                VirtualKeyId = virtualKeyId
            };
            _mockMediaRecordRepository.Setup(x => x.GetByStorageKeyAsync(fileId))
                .ReturnsAsync(mediaRecord);
            
            var fileResult = new FileRetrievalResult
            {
                ContentStream = contentStream,
                Metadata = new FileMetadata
                {
                    FileName = null, // Null filename
                    ContentType = "application/octet-stream",
                    ETag = null // Null ETag
                }
            };

            _mockFileRetrievalService.Setup(x => x.RetrieveFileAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileResult);

            // Act
            var result = await _controller.DownloadFile(fileId);

            // Assert
            var fileActionResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("application/octet-stream", fileActionResult.ContentType);
            Assert.Equal("", fileActionResult.FileDownloadName); // FileStreamResult converts null to empty string
            Assert.False(_controller.Response.Headers.ContainsKey("ETag"));
        }

        #endregion
    }
}