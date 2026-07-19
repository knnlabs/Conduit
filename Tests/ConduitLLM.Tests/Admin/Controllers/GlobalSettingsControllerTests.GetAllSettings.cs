using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class GlobalSettingsControllerTests
    {
        #region GetAllSettings Tests

        [Fact]
        public async Task GetAllSettings_WithSettings_ShouldReturnOkWithList()
        {
            // Arrange
            var settings = new List<GlobalSettingDto>
            {
                new() { Id = 1, Key = "rate_limit", Value = "1000", Description = "Requests per minute" },
                new() { Id = 2, Key = "cache_ttl", Value = "3600", Description = "Cache TTL in seconds" },
                new() { Id = 3, Key = "enable_logging", Value = "true", Description = "Enable detailed logging" }
            };

            _mockService.Setup(x => x.GetAllSettingsAsync())
                .ReturnsAsync(settings);

            // Act
            var result = await _controller.GetAllSettings();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedSettings = okResult.Value.Should().BeAssignableTo<IEnumerable<GlobalSettingDto>>().Subject;
            returnedSettings.Should().HaveCount(3);
            returnedSettings.First().Key.Should().Be("rate_limit");
        }

        [Fact]
        public async Task GetAllSettings_WithEmptyList_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockService.Setup(x => x.GetAllSettingsAsync())
                .ReturnsAsync(new List<GlobalSettingDto>());

            // Act
            var result = await _controller.GetAllSettings();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedSettings = okResult.Value.Should().BeAssignableTo<IEnumerable<GlobalSettingDto>>().Subject;
            returnedSettings.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllSettings_WithException_ShouldPropagateException()
        {
            // Arrange
            _mockService.Setup(x => x.GetAllSettingsAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act + Assert — error mapping is now owned by AdminExceptionMiddleware; the action propagates.
            var act = async () => await _controller.GetAllSettings();
            await act.Should().ThrowAsync<Exception>();
        }

        #endregion
    }
}