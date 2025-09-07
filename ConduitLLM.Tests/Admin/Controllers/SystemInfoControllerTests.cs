using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public class SystemInfoControllerTests
    {
        private readonly Mock<IAdminSystemInfoService> _mockSystemInfoService;
        private readonly Mock<IDiscoveryCacheService> _mockDiscoveryCacheService;
        private readonly Mock<ILogger<SystemInfoController>> _mockLogger;
        private readonly SystemInfoController _controller;

        public SystemInfoControllerTests()
        {
            _mockSystemInfoService = new Mock<IAdminSystemInfoService>();
            _mockDiscoveryCacheService = new Mock<IDiscoveryCacheService>();
            _mockLogger = new Mock<ILogger<SystemInfoController>>();
            
            _controller = new SystemInfoController(
                _mockSystemInfoService.Object,
                _mockLogger.Object,
                _mockDiscoveryCacheService.Object
            );
        }

        [Fact]
        public async Task InvalidateDiscoveryCache_WhenServiceIsAvailable_ReturnsOkResult()
        {
            // Arrange
            _mockDiscoveryCacheService
                .Setup(x => x.InvalidateAllDiscoveryAsync(default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.InvalidateDiscoveryCache();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
            
            // Verify the service was called
            _mockDiscoveryCacheService.Verify(x => x.InvalidateAllDiscoveryAsync(default), Times.Once);
        }

        [Fact]
        public async Task InvalidateDiscoveryCache_WhenServiceIsNull_ReturnsNotImplemented()
        {
            // Arrange
            var controllerWithoutCache = new SystemInfoController(
                _mockSystemInfoService.Object,
                _mockLogger.Object,
                null // No discovery cache service
            );

            // Act
            var result = await controllerWithoutCache.InvalidateDiscoveryCache();

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status501NotImplemented, statusResult.StatusCode);
        }

        [Fact]
        public async Task InvalidateDiscoveryCache_WhenServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var exceptionMessage = "Cache service error";
            _mockDiscoveryCacheService
                .Setup(x => x.InvalidateAllDiscoveryAsync(default))
                .ThrowsAsync(new System.Exception(exceptionMessage));

            // Act
            var result = await _controller.InvalidateDiscoveryCache();

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
            
            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task InvalidateDiscoveryCache_ReturnsProperResponseStructure()
        {
            // Arrange
            _mockDiscoveryCacheService
                .Setup(x => x.InvalidateAllDiscoveryAsync(default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.InvalidateDiscoveryCache();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            // Check the response structure using dynamic
            dynamic response = okResult.Value;
            Assert.NotNull(response.message);
            Assert.NotNull(response.timestamp);
            Assert.NotNull(response.note);
            Assert.Equal("Discovery cache invalidated successfully", response.message);
        }
    }
}