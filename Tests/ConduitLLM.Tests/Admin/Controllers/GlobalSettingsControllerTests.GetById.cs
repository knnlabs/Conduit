using ConduitLLM.Configuration.DTOs;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class GlobalSettingsControllerTests
    {
        #region GetSettingById Tests

        [Fact]
        public async Task GetSettingById_WithExistingId_ShouldReturnOkWithSetting()
        {
            // Arrange
            var setting = new GlobalSettingDto
            {
                Id = 1,
                Key = "rate_limit",
                Value = "1000",
                Description = "Requests per minute"
            };

            _mockService.Setup(x => x.GetSettingByIdAsync(1))
                .ReturnsAsync(setting);

            // Act
            var result = await _controller.GetSettingById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedSetting = Assert.IsType<GlobalSettingDto>(okResult.Value);
            returnedSetting.Id.Should().Be(1);
            returnedSetting.Key.Should().Be("rate_limit");
            returnedSetting.Value.Should().Be("1000");
        }

        [Fact]
        public async Task GetSettingById_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetSettingByIdAsync(999))
                .ReturnsAsync((GlobalSettingDto?)null);

            // Act
            var result = await _controller.GetSettingById(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorObj = notFoundResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("Global setting not found");
        }

        #endregion
    }
}