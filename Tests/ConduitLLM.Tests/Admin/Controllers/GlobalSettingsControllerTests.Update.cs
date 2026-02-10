using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class GlobalSettingsControllerTests
    {
        #region UpdateSetting Tests

        [Fact]
        public async Task UpdateSetting_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingDto
            {
                Id = 1,
                Value = "updated_value",
                Description = "Updated description"
            };

            _mockService.Setup(x => x.UpdateSettingAsync(It.IsAny<UpdateGlobalSettingDto>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateSetting(1, updateDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateSetting_WithMismatchedIds_ShouldReturnBadRequest()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingDto
            {
                Id = 2,
                Value = "value"
            };

            // Act
            var result = await _controller.UpdateSetting(1, updateDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("ID in route must match ID in body");
        }

        [Fact]
        public async Task UpdateSetting_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingDto
            {
                Id = 999,
                Value = "value"
            };

            _mockService.Setup(x => x.UpdateSettingAsync(It.IsAny<UpdateGlobalSettingDto>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateSetting(999, updateDto);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("not_found");
        }

        #endregion

        #region UpdateSettingByKey Tests

        [Fact]
        public async Task UpdateSettingByKey_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingByKeyDto
            {
                Key = "rate_limit",
                Value = "2000",
                Description = "Updated rate limit"
            };

            _mockService.Setup(x => x.UpdateSettingByKeyAsync(It.IsAny<UpdateGlobalSettingByKeyDto>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateSettingByKey(updateDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateSettingByKey_WithFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingByKeyDto
            {
                Key = "some_key",
                Value = "value"
            };

            _mockService.Setup(x => x.UpdateSettingByKeyAsync(It.IsAny<UpdateGlobalSettingByKeyDto>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateSettingByKey(updateDto);

            // Assert
            // Controller throws InvalidOperationException when service returns false,
            // which AdminControllerBase maps to 400 Bad Request
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.Should().Be("Failed to update or create global setting");
            errorResponse.Code.Should().Be("invalid_operation");
        }

        [Fact]
        public async Task UpdateSettingByKey_WithException_ShouldReturn500()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingByKeyDto
            {
                Key = "test_key",
                Value = "value"
            };

            _mockService.Setup(x => x.UpdateSettingByKeyAsync(It.IsAny<UpdateGlobalSettingByKeyDto>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.UpdateSettingByKey(updateDto);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
        }

        #endregion
    }
}