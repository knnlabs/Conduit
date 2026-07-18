using System.Text;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class DownloadsControllerTests
    {
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
            var fileActionResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileActionResult.ContentType.Should().Be("text/plain");
            fileActionResult.FileDownloadName.Should().Be("test.txt");
            fileActionResult.EnableRangeProcessing.Should().BeTrue();
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
            result.Should().BeOfType<FileStreamResult>();
            _controller.Response.Headers.ContainsKey("Content-Disposition").Should().BeFalse();
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
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            var errorDetails = errorResponse.error.Should().BeOfType<ErrorDetailsDto>().Subject;
            errorDetails.Message.Should().Be("File not found");
            errorDetails.Type.Should().Be("not_found");
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

            // Assert — GatewayControllerBase returns OpenAIErrorResponse for unhandled exceptions
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            var errorResponse = statusCodeResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            errorResponse.Error.Should().NotBeNull();
            errorResponse.Error!.Type.Should().Be("server_error");
        }

        #endregion
    }
}