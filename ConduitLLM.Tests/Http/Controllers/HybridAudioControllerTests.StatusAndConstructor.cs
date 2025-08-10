using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class HybridAudioControllerTests
    {
        #region GetStatus Tests

        [Fact]
        public async Task GetStatus_WhenServiceAvailable_ShouldReturnStatusWithMetrics()
        {
            // Arrange
            var metrics = new HybridLatencyMetrics
            {
                AverageSttLatencyMs = 100,
                AverageLlmLatencyMs = 200,
                AverageTtsLatencyMs = 150,
                AverageTotalLatencyMs = 450,
                P95LatencyMs = 600,
                P99LatencyMs = 800,
                SampleCount = 100
            };

            _mockHybridAudioService.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockHybridAudioService.Setup(x => x.GetLatencyMetricsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(metrics);

            // Act
            var result = await _controller.GetStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<HybridAudioController.ServiceStatus>(okResult.Value);
            Assert.True(status.Available);
            Assert.NotNull(status.LatencyMetrics);
            Assert.Equal(metrics.AverageTotalLatencyMs, status.LatencyMetrics.AverageTotalLatencyMs);
        }

        [Fact]
        public async Task GetStatus_WhenServiceUnavailable_ShouldReturnUnavailableStatus()
        {
            // Arrange
            _mockHybridAudioService.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockHybridAudioService.Setup(x => x.GetLatencyMetricsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HybridLatencyMetrics());

            // Act
            var result = await _controller.GetStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<HybridAudioController.ServiceStatus>(okResult.Value);
            Assert.False(status.Available);
        }

        [Fact]
        public async Task GetStatus_WhenExceptionOccurs_ShouldReturnUnavailableStatus()
        {
            // Arrange
            _mockHybridAudioService.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service check failed"));

            // Act
            var result = await _controller.GetStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<HybridAudioController.ServiceStatus>(okResult.Value);
            Assert.False(status.Available);
            Assert.Null(status.LatencyMetrics);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullHybridAudioService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new HybridAudioController(
                null,
                _mockVirtualKeyService.Object,
                _mockLogger.Object));
            Assert.Equal("hybridAudioService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new HybridAudioController(
                _mockHybridAudioService.Object,
                null,
                _mockLogger.Object));
            Assert.Equal("virtualKeyService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new HybridAudioController(
                _mockHybridAudioService.Object,
                _mockVirtualKeyService.Object,
                null));
            Assert.Equal("logger", ex.ParamName);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(HybridAudioController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
            var authAttribute = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorizeAttribute;
            Assert.Equal("VirtualKey", authAttribute.AuthenticationSchemes);
        }

        #endregion
    }
}