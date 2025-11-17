using ConduitLLM.Tests.Admin.TestHelpers;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class GlobalSettingsControllerTests
    {
        #region DeleteSetting Tests

        [Fact]
        public async Task DeleteSetting_WithExistingId_ShouldReturnNoContent()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteSetting(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteSetting_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteSetting(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorObj = notFoundResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("Global setting not found");
        }

        #endregion

        #region DeleteSettingByKey Tests

        [Fact]
        public async Task DeleteSettingByKey_WithExistingKey_ShouldReturnNoContent()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingByKeyAsync("rate_limit"))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteSettingByKey("rate_limit");

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteSettingByKey_WithNonExistingKey_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingByKeyAsync("non_existing"))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteSettingByKey("non_existing");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorObj = notFoundResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("Global setting not found");
        }

        [Fact]
        public async Task DeleteSettingByKey_WithException_ShouldReturn500()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingByKeyAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.DeleteSettingByKey("test_key");

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            
            _mockLogger.VerifyLogWithAnyException(LogLevel.Error, "Error deleting global setting with key");
        }

        #endregion
    }
}