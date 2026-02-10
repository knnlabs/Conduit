using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Events;
using FluentAssertions;
using MassTransit;
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
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<SystemInfoController>> _mockLogger;
        private readonly SystemInfoController _controller;

        public SystemInfoControllerTests()
        {
            _mockSystemInfoService = new Mock<IAdminSystemInfoService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<SystemInfoController>>();

            _controller = new SystemInfoController(
                _mockSystemInfoService.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task InvalidateDiscoveryCache_PublishesEventAndReturnsOkResult()
        {
            // Arrange
            _mockPublishEndpoint
                .Setup(x => x.Publish(It.IsAny<DiscoveryCacheInvalidationRequested>(), default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.InvalidateDiscoveryCache();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            // Verify the event was published
            _mockPublishEndpoint.Verify(
                x => x.Publish(
                    It.Is<DiscoveryCacheInvalidationRequested>(e =>
                        e.Reason == "Manual invalidation via Admin API" &&
                        e.RequestedBy == "Admin User"),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task InvalidateDiscoveryCache_WhenPublishThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var exceptionMessage = "Event publishing failed";
            _mockPublishEndpoint
                .Setup(x => x.Publish(It.IsAny<DiscoveryCacheInvalidationRequested>(), default))
                .ThrowsAsync(new System.Exception(exceptionMessage));

            // Act
            var result = await _controller.InvalidateDiscoveryCache();

            // Assert
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
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
            _mockPublishEndpoint
                .Setup(x => x.Publish(It.IsAny<DiscoveryCacheInvalidationRequested>(), default))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.InvalidateDiscoveryCache();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            Assert.NotNull(okResult.Value);

            // Check the response structure using JSON serialization
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var response = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            Assert.NotNull(response);
            Assert.True(response.ContainsKey("message"));
            Assert.True(response.ContainsKey("timestamp"));
            Assert.True(response.ContainsKey("note"));
            Assert.Equal("Discovery cache invalidation request published successfully", response["message"].ToString());
        }
    }
}