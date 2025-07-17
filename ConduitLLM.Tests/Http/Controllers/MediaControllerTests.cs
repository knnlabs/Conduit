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
    public class MediaControllerTests
    {
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<ILogger<MediaController>> _mockLogger;
        private readonly MediaController _controller;

        public MediaControllerTests()
        {
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockLogger = new Mock<ILogger<MediaController>>();
            _controller = new MediaController(_mockStorageService.Object, _mockLogger.Object);
        }

        #region GetMedia Tests

        [Fact]
        public async Task GetMedia_WithValidKey_ShouldReturnFile()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";
            var testContent = "test image content";
            var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
            
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

        #endregion

        #region GetMediaInfo Tests

        [Fact]
        public async Task GetMediaInfo_WithValidKey_ShouldReturnMediaInfo()
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
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                ExpiresAt = DateTime.UtcNow.AddHours(23),
                CustomMetadata = new Dictionary<string, string>
                {
                    ["source"] = "test-source",
                    ["generated-by"] = "test-model"
                }
            };

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            // Act
            var result = await _controller.GetMediaInfo(storageKey);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.Equal(mediaInfo, okResult.Value);
        }

        [Fact]
        public async Task GetMediaInfo_WithNonExistentKey_ShouldReturnNotFound()
        {
            // Arrange
            var storageKey = "non-existent-key";

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync((MediaInfo)null);

            // Act
            var result = await _controller.GetMediaInfo(storageKey);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetMediaInfo_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            var storageKey = "test-key";

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ThrowsAsync(new Exception("Storage error"));

            // Act
            var result = await _controller.GetMediaInfo(storageKey);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Equal("An error occurred while retrieving media information", objectResult.Value);
        }

        #endregion

        #region CheckMediaExists Tests

        [Fact]
        public async Task CheckMediaExists_WithExistingKey_ShouldReturnOk()
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

            _mockStorageService.Setup(x => x.ExistsAsync(storageKey))
                .ReturnsAsync(true);

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync(mediaInfo);

            // Setup controller context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.CheckMediaExists(storageKey);

            // Assert
            Assert.IsType<OkResult>(result);
            
            // Verify headers are set
            Assert.Equal("image/jpeg", _controller.Response.Headers["Content-Type"]);
            Assert.Equal("1000", _controller.Response.Headers["Content-Length"]);
        }

        [Fact]
        public async Task CheckMediaExists_WithNonExistentKey_ShouldReturnNotFound()
        {
            // Arrange
            var storageKey = "non-existent-key";

            _mockStorageService.Setup(x => x.ExistsAsync(storageKey))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CheckMediaExists(storageKey);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CheckMediaExists_WithExistingKeyButNoInfo_ShouldReturnOkWithoutHeaders()
        {
            // Arrange
            var storageKey = "image/2023/01/01/test-hash.jpg";

            _mockStorageService.Setup(x => x.ExistsAsync(storageKey))
                .ReturnsAsync(true);

            _mockStorageService.Setup(x => x.GetInfoAsync(storageKey))
                .ReturnsAsync((MediaInfo)null);

            // Setup controller context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.CheckMediaExists(storageKey);

            // Assert
            Assert.IsType<OkResult>(result);
            
            // Verify no headers are set when media info is null
            Assert.False(_controller.Response.Headers.ContainsKey("Content-Type"));
            Assert.False(_controller.Response.Headers.ContainsKey("Content-Length"));
        }

        [Fact]
        public async Task CheckMediaExists_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            var storageKey = "test-key";

            _mockStorageService.Setup(x => x.ExistsAsync(storageKey))
                .ThrowsAsync(new Exception("Storage error"));

            // Act
            var result = await _controller.CheckMediaExists(storageKey);

            // Assert
            Assert.IsType<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.Equal(500, objectResult.StatusCode);
        }

        #endregion

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
            Assert.IsType<BadRequestObjectResult>(result);
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
        [InlineData("")]
        public async Task ParseRangeHeader_WithInvalidRanges_ShouldReturnBadRequest(string rangeHeader)
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
            Assert.IsType<BadRequestObjectResult>(result);
        }

        #endregion

        #region Content Stream Tests

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