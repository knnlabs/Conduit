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
        #region HandleVideoRangeRequest Tests

        [Fact]
        public async Task HandleVideoRangeRequest_WithValidRange_ShouldReturnPartialContent()
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

            _mockStorageService.Setup(x => x.GetVideoStreamAsync(storageKey, 0, 999))
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
            
            // Verify partial content status and headers
            Assert.Equal(206, _controller.Response.StatusCode);
            Assert.Equal("bytes", _controller.Response.Headers["Accept-Ranges"]);
            Assert.Equal("bytes 0-999/1000000", _controller.Response.Headers["Content-Range"]);
            Assert.Equal("1000", _controller.Response.Headers["Content-Length"]);
        }

        [Fact]
        public async Task HandleVideoRangeRequest_WithInvalidRange_ShouldReturnRangeNotSatisfiable()
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "video/mp4",
                SizeBytes = 1000,
                FileName = "test.mp4",
                MediaType = MediaType.Video,
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            // Setup controller context with invalid Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = "bytes=2000-3000"; // Beyond file size

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.Equal(416, objectResult.StatusCode);
        }

        [Fact]
        public async Task HandleVideoRangeRequest_WithMalformedRange_ShouldReturnBadRequest()
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

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            // Setup controller context with malformed Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = "invalid-range-header";

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(416, objectResult.StatusCode);
        }

        [Fact]
        public async Task HandleVideoRangeRequest_WithNonExistentVideo_ShouldReturnNotFound()
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

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetVideoStreamAsync(storageKey, It.IsAny<long>(), It.IsAny<long>()))
                .ReturnsAsync((RangedStream)null);

            // Setup controller context with Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = "bytes=0-999";

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task HandleVideoRangeRequest_WithException_ShouldReturnInternalServerError()
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

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetVideoStreamAsync(storageKey, It.IsAny<long>(), It.IsAny<long>()))
                .ThrowsAsync(new Exception("Storage error"));

            // Setup controller context with Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = "bytes=0-999";

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.Equal(500, objectResult.StatusCode);
        }

        #endregion

        #region Range Header Parsing Tests

        [Theory]
        [InlineData("bytes=0-999", 1000, 0, 999)]
        [InlineData("bytes=0-", 1000, 0, 999)]
        [InlineData("bytes=500-", 1000, 500, 999)]
        [InlineData("bytes=0-499", 1000, 0, 499)]
        [InlineData("bytes=500-999", 1000, 500, 999)]
        public async Task ParseRangeHeader_WithValidRanges_ShouldParseCorrectly(
            string rangeHeader, long totalSize, long expectedStart, long expectedEnd)
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "video/mp4",
                SizeBytes = totalSize,
                FileName = "test.mp4",
                MediaType = MediaType.Video,
                CreatedAt = DateTime.UtcNow
            };

            var rangedStream = new RangedStream
            {
                Stream = new MemoryStream(new byte[expectedEnd - expectedStart + 1]),
                RangeStart = expectedStart,
                RangeEnd = expectedEnd,
                TotalSize = totalSize,
                ContentType = "video/mp4"
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            _mockStorageService.Setup(x => x.GetVideoStreamAsync(storageKey, expectedStart, expectedEnd))
                .ReturnsAsync(rangedStream);

            // Setup controller context with Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = rangeHeader;

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<FileStreamResult>(result);
            
            // Verify the correct range was requested
            _mockStorageService.Verify(x => x.GetVideoStreamAsync(storageKey, expectedStart, expectedEnd), 
                Times.Once);
        }

        [Theory]
        [InlineData("invalid-range")]
        [InlineData("bytes=")]
        [InlineData("bytes=invalid")]
        [InlineData("bytes=0-invalid")]
        [InlineData("bytes=invalid-999")]
        [InlineData("range=0-999")]
        public async Task ParseRangeHeader_WithInvalidRanges_ShouldReturn416RangeNotSatisfiable(string rangeHeader)
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "video/mp4",
                SizeBytes = 1000,
                FileName = "test.mp4",
                MediaType = MediaType.Video,
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            // Setup controller context with invalid Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = rangeHeader;

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(416, objectResult.StatusCode);
        }

        [Fact]
        public async Task ParseRangeHeader_WithEmptyRange_ShouldReturnBadRequest()
        {
            // Arrange
            var storageKey = "video/2023/01/01/test-hash.mp4";
            var mediaInfo = new MediaInfo
            {
                StorageKey = storageKey,
                ContentType = "video/mp4",
                SizeBytes = 1000,
                FileName = "test.mp4",
                MediaType = MediaType.Video,
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            // Setup controller context with empty Range header
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _controller.Request.Headers["Range"] = "";

            // Act
            var result = await _controller.GetMedia(storageKey);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        #endregion
    }
}