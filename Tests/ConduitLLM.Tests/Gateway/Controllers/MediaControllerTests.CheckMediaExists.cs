using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class MediaControllerTests
    {
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
            var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        #endregion
    }
}