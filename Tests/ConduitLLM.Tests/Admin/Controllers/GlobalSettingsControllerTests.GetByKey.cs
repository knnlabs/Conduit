using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class GlobalSettingsControllerTests
    {
        #region GetSettingByKey Tests

        [Fact]
        public async Task GetSettingByKey_WithExistingKey_ShouldReturnOkWithSetting()
        {
            // Arrange
            var setting = new GlobalSettingDto
            {
                Id = 1,
                Key = "rate_limit",
                Value = "1000",
                Description = "Requests per minute"
            };

            _mockService.Setup(x => x.GetSettingByKeyAsync("rate_limit"))
                .ReturnsAsync(setting);

            // Act
            var result = await _controller.GetSettingByKey("rate_limit");

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedSetting = okResult.Value.Should().BeOfType<GlobalSettingDto>().Subject;
            returnedSetting.Key.Should().Be("rate_limit");
        }

        [Fact]
        public async Task GetSettingByKey_WithNonExistingKey_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetSettingByKeyAsync("non_existing"))
                .ReturnsAsync((GlobalSettingDto?)null);

            // Act
            var result = await _controller.GetSettingByKey("non_existing");

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
            var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("not_found");
        }

        [Fact]
        public async Task GetSettingByKey_WithException_ShouldReturn500()
        {
            // Arrange
            _mockService.Setup(x => x.GetSettingByKeyAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetSettingByKey("test_key");

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
        }

        #endregion
    }
}