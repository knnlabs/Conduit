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
    }
}