using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Cache;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ConfigurationControllerLLMCacheTests
    {
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockDbContextFactory;
        private readonly Mock<ILogger<ConfigurationController>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILLMCacheManagementService> _mockLLMCacheManagementService;
        private readonly ConfigurationController _controller;

        public ConfigurationControllerLLMCacheTests()
        {
            _mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            _mockLogger = new Mock<ILogger<ConfigurationController>>();
            _mockCache = new Mock<IMemoryCache>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLLMCacheManagementService = new Mock<ILLMCacheManagementService>();

            _controller = new ConfigurationController(
                _mockDbContextFactory.Object,
                _mockLogger.Object,
                _mockCache.Object,
                _mockConfiguration.Object,
                _mockLLMCacheManagementService.Object);
        }

        private void SetupControllerUser(string userName)
        {
            var claims = new[] { new Claim(ClaimTypes.Name, userName) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region GetLLMCacheStatus Tests

        [Fact]
        public async Task GetLLMCacheStatus_ReturnsOkWithStatus()
        {
            // Arrange
            var expectedStatus = new LLMCacheControlDto
            {
                Enabled = false,
                LastChangedAt = DateTime.UtcNow.AddHours(-2),
                LastChangedBy = "admin",
                LastChangeReason = "Testing",
                ActiveInstances = 3
            };

            _mockLLMCacheManagementService
                .Setup(x => x.GetLLMCacheStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStatus);

            // Act
            var result = await _controller.GetLLMCacheStatus();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var status = okResult.Value.Should().BeOfType<LLMCacheControlDto>().Subject;
            status.Enabled.Should().Be(expectedStatus.Enabled);
            status.LastChangedBy.Should().Be(expectedStatus.LastChangedBy);
            status.LastChangeReason.Should().Be(expectedStatus.LastChangeReason);

            _mockLLMCacheManagementService.Verify(
                x => x.GetLLMCacheStatusAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetLLMCacheStatus_ServiceThrowsException_Returns500()
        {
            // Arrange
            _mockLLMCacheManagementService
                .Setup(x => x.GetLLMCacheStatusAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _controller.GetLLMCacheStatus();

            // Assert - AdminControllerBase returns ObjectResult with ErrorResponseDto
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);

            var errorResponse = statusResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.Should().Be("An unexpected error occurred.");
            errorResponse.Code.Should().Be("internal_error");
        }

        [Fact]
        public async Task GetLLMCacheStatus_LogsError_WhenExceptionOccurs()
        {
            // Arrange
            var exception = new Exception("Test error");
            _mockLLMCacheManagementService
                .Setup(x => x.GetLLMCacheStatusAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            await _controller.GetLLMCacheStatus();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => true),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region ToggleLLMCache Tests

        [Fact]
        public async Task ToggleLLMCache_EnableCache_ReturnsOkWithUpdatedStatus()
        {
            // Arrange
            SetupControllerUser("test-admin");

            var request = new ToggleLLMCacheRequest
            {
                Enabled = true,
                Reason = "Enabling for production"
            };

            var expectedResult = new LLMCacheControlDto
            {
                Enabled = true,
                LastChangedAt = DateTime.UtcNow,
                LastChangedBy = "test-admin",
                LastChangeReason = "Enabling for production",
                ActiveInstances = null
            };

            _mockLLMCacheManagementService
                .Setup(x => x.ToggleLLMCacheAsync(true, "test-admin", "Enabling for production", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ToggleLLMCache(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var status = okResult.Value.Should().BeOfType<LLMCacheControlDto>().Subject;
            status.Enabled.Should().BeTrue();
            status.LastChangedBy.Should().Be("test-admin");
            status.LastChangeReason.Should().Be("Enabling for production");

            _mockLLMCacheManagementService.Verify(
                x => x.ToggleLLMCacheAsync(true, "test-admin", "Enabling for production", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ToggleLLMCache_DisableCache_ReturnsOkWithUpdatedStatus()
        {
            // Arrange
            SetupControllerUser("admin@example.com");

            var request = new ToggleLLMCacheRequest
            {
                Enabled = false,
                Reason = "Performance issues detected"
            };

            var expectedResult = new LLMCacheControlDto
            {
                Enabled = false,
                LastChangedAt = DateTime.UtcNow,
                LastChangedBy = "admin@example.com",
                LastChangeReason = "Performance issues detected",
                ActiveInstances = null
            };

            _mockLLMCacheManagementService
                .Setup(x => x.ToggleLLMCacheAsync(false, "admin@example.com", "Performance issues detected", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ToggleLLMCache(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var status = okResult.Value.Should().BeOfType<LLMCacheControlDto>().Subject;
            status.Enabled.Should().BeFalse();
            status.LastChangedBy.Should().Be("admin@example.com");
        }

        [Fact]
        public async Task ToggleLLMCache_NoReason_PassesNullToService()
        {
            // Arrange
            SetupControllerUser("admin");

            var request = new ToggleLLMCacheRequest
            {
                Enabled = true,
                Reason = null
            };

            var expectedResult = new LLMCacheControlDto
            {
                Enabled = true,
                LastChangedAt = DateTime.UtcNow,
                LastChangedBy = "admin",
                LastChangeReason = null,
                ActiveInstances = null
            };

            _mockLLMCacheManagementService
                .Setup(x => x.ToggleLLMCacheAsync(true, "admin", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ToggleLLMCache(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var status = okResult.Value.Should().BeOfType<LLMCacheControlDto>().Subject;
            status.LastChangeReason.Should().BeNull();

            _mockLLMCacheManagementService.Verify(
                x => x.ToggleLLMCacheAsync(true, "admin", null, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ToggleLLMCache_UserNotAuthenticated_UsesUnknown()
        {
            // Arrange
            // Don't set up user - User.Identity.Name will be null
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new ToggleLLMCacheRequest
            {
                Enabled = true,
                Reason = "Test"
            };

            var expectedResult = new LLMCacheControlDto
            {
                Enabled = true,
                LastChangedAt = DateTime.UtcNow,
                LastChangedBy = "Unknown",
                LastChangeReason = "Test",
                ActiveInstances = null
            };

            _mockLLMCacheManagementService
                .Setup(x => x.ToggleLLMCacheAsync(true, "Unknown", "Test", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ToggleLLMCache(request);

            // Assert
            _mockLLMCacheManagementService.Verify(
                x => x.ToggleLLMCacheAsync(true, "Unknown", "Test", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ToggleLLMCache_ServiceThrowsException_Returns500()
        {
            // Arrange
            SetupControllerUser("admin");

            var request = new ToggleLLMCacheRequest
            {
                Enabled = true,
                Reason = "Test"
            };

            _mockLLMCacheManagementService
                .Setup(x => x.ToggleLLMCacheAsync(It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Event bus unavailable"));

            // Act
            var result = await _controller.ToggleLLMCache(request);

            // Assert - AdminControllerBase returns ObjectResult with ErrorResponseDto
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);

            var errorResponse = statusResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.Should().Be("An unexpected error occurred.");
            errorResponse.Code.Should().Be("internal_error");
        }

        [Fact]
        public async Task ToggleLLMCache_LogsError_WhenExceptionOccurs()
        {
            // Arrange
            SetupControllerUser("admin");

            var request = new ToggleLLMCacheRequest
            {
                Enabled = true
            };

            var exception = new Exception("Test error");
            _mockLLMCacheManagementService
                .Setup(x => x.ToggleLLMCacheAsync(It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            await _controller.ToggleLLMCache(request);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => true),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}
