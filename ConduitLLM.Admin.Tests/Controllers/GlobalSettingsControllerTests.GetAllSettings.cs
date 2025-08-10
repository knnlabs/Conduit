using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Tests.TestHelpers;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Controllers
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
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedSettings = Assert.IsAssignableFrom<IEnumerable<GlobalSettingDto>>(okResult.Value);
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
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedSettings = Assert.IsAssignableFrom<IEnumerable<GlobalSettingDto>>(okResult.Value);
            returnedSettings.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllSettings_WithException_ShouldReturn500()
        {
            // Arrange
            _mockService.Setup(x => x.GetAllSettingsAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllSettings();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            statusCodeResult.Value.Should().Be("An unexpected error occurred.");
            
            _mockLogger.VerifyLogWithAnyException(LogLevel.Error, "Error getting all global settings");
        }

        #endregion
    }
}