using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class MediaControllerTests
    {
        #region GetMedia Tests

        [Fact]
        public async Task GetMedia_WithValidKey_ShouldReturnFile()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var testContent = "test image content";
            var contentBytes = Encoding.UTF8.GetBytes(testContent);
            var contentStream = new MemoryStream(contentBytes, false);
            
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "image/jpeg",
                SizeBytes = testContent.Length,
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetStreamAsync(storageKey))
                .ReturnsAsync(contentStream);

            // Setup controller context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<FileStreamResult>(result);
            var fileResult = result as FileStreamResult;
            Assert.NotNull(fileResult);
            Assert.Equal("image/jpeg", fileResult.ContentType);
            Assert.Equal(contentStream, fileResult.FileStream);
            Assert.True(fileResult.EnableRangeProcessing);
        }

        [Fact]
        public async Task GetMedia_WithVideoAndRangeHeader_ShouldCallHandleVideoRangeRequest()
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "video/mp4",
                SizeBytes = 1000000,
                FileName = "test.mp4",
                MediaType = MediaType.Video,
                CreatedAt = DateTime.UtcNow
            };

            var rangedStream = new RangedStream
            {
                Stream = new MemoryStream(new byte[1000]),
                RangeStart = 0,
                RangeEnd = 999,
                TotalSize = 1000000,
                ContentType = "video/mp4"
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetVideoStreamAsync(storageKey, It.IsAny<long?>(), It.IsAny<long?>()))
                .ReturnsAsync(rangedStream);

            // Setup controller context with Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = "bytes=0-999";

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<FileStreamResult>(result);
            var fileResult = result as FileStreamResult;
            Assert.NotNull(fileResult);
            Assert.Equal("video/mp4", fileResult.ContentType);
            Assert.Equal(rangedStream.Stream, fileResult.FileStream);

            // Verify video stream was called with range
            _mockStorageService.Verify(x => x.GetVideoStreamAsync(storageKey, It.IsAny<long>(), It.IsAny<long>()), 
                Times.Once);
        }

        [Fact]
        public async Task GetMedia_WithVideoFile_ShouldSetVideoHeaders()
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "video/mp4",
                SizeBytes = 1000000,
                FileName = "test.mp4",
                MediaType = MediaType.Video,
                CreatedAt = DateTime.UtcNow
            };

            var contentStream = new MemoryStream(new byte[1000]);

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetStreamAsync(storageKey))
                .ReturnsAsync(contentStream);

            // Setup controller context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<FileStreamResult>(result);
            
            // Verify video-specific headers are set
            Assert.Equal("bytes", _controller.Response.Headers["Accept-Ranges"]);
            Assert.Equal("*", _controller.Response.Headers["Access-Control-Allow-Origin"]);
            Assert.Equal("GET, HEAD, OPTIONS", _controller.Response.Headers["Access-Control-Allow-Methods"]);
            Assert.Equal("Range", _controller.Response.Headers["Access-Control-Allow-Headers"]);
        }

        [Fact]
        public async Task GetMedia_WithNonExistentKey_ShouldReturnNotFound()
        {
            // Arrange
            var storageKey = "non-existent-key";

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync((MediaInfo)null);

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetMedia_WithEmptyKey_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.GetMedia("");

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.Equal("Invalid storage key", badRequestResult.Value);
        }

        [Fact]
        public async Task GetMedia_WithNullKey_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.GetMedia(null);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.Equal("Invalid storage key", badRequestResult.Value);
        }

        [Fact]
        public async Task GetMedia_WithWhitespaceKey_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.GetMedia("   ");

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.Equal("Invalid storage key", badRequestResult.Value);
        }

        [Fact]
        public async Task GetMedia_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            var storageKey = "test-key";

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ThrowsAsync(new Exception("Storage error"));

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("An error occurred while retrieving the media", objectResult.Value);
        }

        [Fact]
        public async Task GetMedia_WithValidKey_ShouldSetCacheHeaders()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "image/jpeg",
                SizeBytes = 1000,
                FileName = "test.jpg",
                MediaType = MediaType.Image,
                CreatedAt = DateTime.UtcNow
            };

            var contentStream = new MemoryStream(new byte[1000]);

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetStreamAsync(storageKey))
                .ReturnsAsync(contentStream);

            // Setup controller context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<FileStreamResult>(result);
            
            // Verify cache headers are set
            Assert.Equal("public, max-age=3600", _controller.Response.Headers["Cache-Control"]);
            Assert.Equal($"\"{storageKey}\"", _controller.Response.Headers["ETag"]);
        }

        [Fact]
        public async Task GetMedia_WithStreamReturnedNull_ShouldReturnNotFound()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
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
                .ReturnsAsync((Stream)null);

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        #endregion
    }
}