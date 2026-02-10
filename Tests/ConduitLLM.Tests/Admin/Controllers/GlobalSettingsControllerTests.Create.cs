using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class GlobalSettingsControllerTests
    {
        #region CreateSetting Tests

        [Fact]
        public async Task CreateSetting_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var createDto = new CreateGlobalSettingDto
            {
                Key = "new_setting",
                Value = "new_value",
                Description = "A new setting"
            };

            var createdDto = new GlobalSettingDto
            {
                Id = 10,
                Key = createDto.Key,
                Value = createDto.Value,
                Description = createDto.Description
            };

            _mockService.Setup(x => x.CreateSettingAsync(It.IsAny<CreateGlobalSettingDto>()))
                .ReturnsAsync(createdDto);

            // Act
            var result = await _controller.CreateSetting(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(GlobalSettingsController.GetSettingById));
            createdResult.RouteValues!["id"].Should().Be(10);

            var returnedSetting = createdResult.Value.Should().BeOfType<GlobalSettingDto>().Subject;
            returnedSetting.Key.Should().Be("new_setting");
        }

        [Fact]
        public async Task CreateSetting_WithDuplicateKey_ShouldReturnBadRequest()
        {
            // Arrange
            var createDto = new CreateGlobalSettingDto
            {
                Key = "existing_key",
                Value = "value"
            };

            _mockService.Setup(x => x.CreateSettingAsync(It.IsAny<CreateGlobalSettingDto>()))
                .ThrowsAsync(new InvalidOperationException("Setting with key already exists"));

            // Act
            var result = await _controller.CreateSetting(createDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.Should().Be("Setting with key already exists");
            errorResponse.Code.Should().Be("invalid_operation");
        }

        #endregion
    }
}