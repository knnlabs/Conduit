using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
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

            _mockService.Setup(x => x.GetSettingByIdAsync(1))
                .ReturnsAsync(new GlobalSettingDto { Id = 1, Key = "test_key", Value = "old_value", Description = "Old description" });
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
        public async Task UpdateSetting_WithNonExistingId_ShouldPropagateException()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingDto
            {
                Id = 999,
                Value = "value"
            };

            _mockService.Setup(x => x.GetSettingByIdAsync(999))
                .ReturnsAsync((GlobalSettingDto?)null);

            // Act + Assert — error mapping is now owned by AdminExceptionMiddleware; the action propagates.
            var act = async () => await _controller.UpdateSetting(999, updateDto);
            await act.Should().ThrowAsync<KeyNotFoundException>();
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
        public async Task UpdateSettingByKey_WithFailure_ShouldPropagateException()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingByKeyDto
            {
                Key = "some_key",
                Value = "value"
            };

            _mockService.Setup(x => x.UpdateSettingByKeyAsync(It.IsAny<UpdateGlobalSettingByKeyDto>()))
                .ReturnsAsync(false);

            // Act + Assert — controller throws InvalidOperationException when service returns false;
            // error mapping is now owned by AdminExceptionMiddleware, so the action propagates.
            var act = async () => await _controller.UpdateSettingByKey(updateDto);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateSettingByKey_WithException_ShouldPropagateException()
        {
            // Arrange
            var updateDto = new UpdateGlobalSettingByKeyDto
            {
                Key = "test_key",
                Value = "value"
            };

            _mockService.Setup(x => x.UpdateSettingByKeyAsync(It.IsAny<UpdateGlobalSettingByKeyDto>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act + Assert — error mapping is now owned by AdminExceptionMiddleware; the action propagates.
            var act = async () => await _controller.UpdateSettingByKey(updateDto);
            await act.Should().ThrowAsync<Exception>();
        }

        #endregion
    }
}