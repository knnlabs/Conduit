using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class DownloadsControllerTests
    {
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
    }
}