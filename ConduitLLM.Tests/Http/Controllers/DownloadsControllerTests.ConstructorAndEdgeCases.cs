using System;
using System.Collections.Generic;
using System.IO;
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