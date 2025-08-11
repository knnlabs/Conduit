using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using ConduitLLM.Configuration.DTOs;

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
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            var errorDetails = Assert.IsType<ErrorDetailsDto>(errorResponse.error);
            Assert.Equal("File not found", errorDetails.Message);
            Assert.Equal("not_found", errorDetails.Type);
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
            var errorResponse = Assert.IsType<ErrorResponseDto>(statusCodeResult.Value);
            var errorDetails = Assert.IsType<ErrorDetailsDto>(errorResponse.error);
            Assert.Equal("An error occurred while downloading the file", errorDetails.Message);
            Assert.Equal("server_error", errorDetails.Type);
        }

        #endregion
    }
}