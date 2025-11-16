using ConduitLLM.Admin.Controllers;
using ConduitLLM.Tests.Admin.TestHelpers;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            createdResult.ActionName.Should().Be(nameof(GlobalSettingsController.GetSettingById));
            createdResult.RouteValues!["id"].Should().Be(10);
            
            var returnedSetting = Assert.IsType<GlobalSettingDto>(createdResult.Value);
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
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("Setting with key already exists");
            
            _mockLogger.VerifyLogWithAnyException(LogLevel.Warning, "Invalid operation when creating global setting");
        }

        #endregion
    }
}