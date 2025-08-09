using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class DownloadsControllerTests
    {
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
    }
}