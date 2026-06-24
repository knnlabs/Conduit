using ConduitLLM.Configuration.DTOs;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

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
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task DeleteSetting_WithNonExistingId_ShouldPropagateException()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingAsync(999))
                .ReturnsAsync(false);

            // Act + Assert — error mapping is now owned by AdminExceptionMiddleware; the action propagates.
            var act = async () => await _controller.DeleteSetting(999);
            await act.Should().ThrowAsync<KeyNotFoundException>();
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
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task DeleteSettingByKey_WithNonExistingKey_ShouldPropagateException()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingByKeyAsync("non_existing"))
                .ReturnsAsync(false);

            // Act + Assert — error mapping is now owned by AdminExceptionMiddleware; the action propagates.
            var act = async () => await _controller.DeleteSettingByKey("non_existing");
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task DeleteSettingByKey_WithException_ShouldPropagateException()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteSettingByKeyAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act + Assert — error mapping is now owned by AdminExceptionMiddleware; the action propagates.
            var act = async () => await _controller.DeleteSettingByKey("test_key");
            await act.Should().ThrowAsync<Exception>();
        }

        #endregion
    }
}