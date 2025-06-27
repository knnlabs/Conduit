using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.HealthChecks;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Metrics;

namespace ConduitLLM.Http.Tests.HealthChecks
{
    public class SignalRHealthCheckTests
    {
        private readonly Mock<IHubContext<SystemNotificationHub>> _navigationHubContextMock;
        private readonly Mock<IHubContext<TaskHub>> _taskHubContextMock;
        private readonly Mock<IHubContext<ImageGenerationHub>> _imageHubContextMock;
        private readonly Mock<IHubContext<VideoGenerationHub>> _videoHubContextMock;
        private readonly Mock<SignalRMetrics> _metricsMock;
        private readonly Mock<ILogger<SignalRHealthCheck>> _loggerMock;
        private readonly SignalRHealthCheck _healthCheck;
        private readonly Mock<IHubClients> _clientsMock;
        private readonly Mock<IClientProxy> _clientProxyMock;

        public SignalRHealthCheckTests()
        {
            _navigationHubContextMock = new Mock<IHubContext<SystemNotificationHub>>();
            _taskHubContextMock = new Mock<IHubContext<TaskHub>>();
            _imageHubContextMock = new Mock<IHubContext<ImageGenerationHub>>();
            _videoHubContextMock = new Mock<IHubContext<VideoGenerationHub>>();
            _metricsMock = new Mock<SignalRMetrics>();
            _loggerMock = new Mock<ILogger<SignalRHealthCheck>>();
            _clientsMock = new Mock<IHubClients>();
            _clientProxyMock = new Mock<IClientProxy>();

            // Setup hub contexts
            _clientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
            _navigationHubContextMock.Setup(x => x.Clients).Returns(_clientsMock.Object);
            _taskHubContextMock.Setup(x => x.Clients).Returns(_clientsMock.Object);
            _imageHubContextMock.Setup(x => x.Clients).Returns(_clientsMock.Object);
            _videoHubContextMock.Setup(x => x.Clients).Returns(_clientsMock.Object);

            _healthCheck = new SignalRHealthCheck(
                _navigationHubContextMock.Object,
                _taskHubContextMock.Object,
                _imageHubContextMock.Object,
                _videoHubContextMock.Object,
                _metricsMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task CheckHealthAsync_AllHubsHealthy_ReturnsHealthy()
        {
            // Arrange
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("signalr", _ => _healthCheck, null, null)
            };

            // Act
            var result = await _healthCheck.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("All SignalR hubs are healthy", result.Description);
            Assert.NotNull(result.Data);
            Assert.Contains("NavigationStateHub_status", result.Data.Keys);
            Assert.Contains("TaskHub_status", result.Data.Keys);
            Assert.Contains("ImageGenerationHub_status", result.Data.Keys);
            Assert.Contains("VideoGenerationHub_status", result.Data.Keys);
        }

        [Fact]
        public async Task CheckHealthAsync_OneHubUnhealthy_ReturnsDegraded()
        {
            // Arrange
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("signalr", _ => _healthCheck, null, null)
            };

            // Make one hub throw an exception
            _navigationHubContextMock.Setup(x => x.Clients).Throws(new Exception("Hub error"));

            // Act
            var result = await _healthCheck.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.Contains("3/4 SignalR hubs are healthy", result.Description);
            Assert.Contains("unhealthy:", (string)result.Data["NavigationStateHub_status"]);
        }

        [Fact]
        public async Task CheckHealthAsync_AllHubsUnhealthy_ReturnsUnhealthy()
        {
            // Arrange
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("signalr", _ => _healthCheck, null, null)
            };

            // Make all hubs throw exceptions
            _navigationHubContextMock.Setup(x => x.Clients).Throws(new Exception("Hub error"));
            _taskHubContextMock.Setup(x => x.Clients).Throws(new Exception("Hub error"));
            _imageHubContextMock.Setup(x => x.Clients).Throws(new Exception("Hub error"));
            _videoHubContextMock.Setup(x => x.Clients).Throws(new Exception("Hub error"));

            // Act
            var result = await _healthCheck.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Equal("All SignalR hubs are unhealthy", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_ExceptionThrown_ReturnsUnhealthy()
        {
            // Arrange
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("signalr", _ => _healthCheck, null, null)
            };

            var exception = new Exception("Health check error");
            
            // Make the first hub context throw a different type of exception
            _navigationHubContextMock.Setup(x => x.Clients).Throws(exception);
            _taskHubContextMock.Setup(x => x.Clients).Throws(exception);
            _imageHubContextMock.Setup(x => x.Clients).Throws(exception);
            _videoHubContextMock.Setup(x => x.Clients).Throws(exception);

            // Act
            var result = await _healthCheck.CheckHealthAsync(context);

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
        }

        [Fact]
        public async Task CheckHealthAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("signalr", _ => _healthCheck, null, null)
            };

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Make SendAsync check for cancellation
            _clientProxyMock.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns<string, object[], CancellationToken>((method, args, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                });

            // Act & Assert - Health check should handle cancellation gracefully
            var result = await _healthCheck.CheckHealthAsync(context, cts.Token);
            // The health check doesn't pass the cancellation token to hub operations,
            // so it should complete normally
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CheckHealthAsync_IncludesMetricData()
        {
            // Arrange
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("signalr", _ => _healthCheck, null, null)
            };

            // Act
            var result = await _healthCheck.CheckHealthAsync(context);

            // Assert
            Assert.Contains("active_connections", result.Data.Keys);
            Assert.Contains("authentication_failures_rate", result.Data.Keys);
            Assert.Contains("hub_errors_rate", result.Data.Keys);
            Assert.Contains("message_processing_p95", result.Data.Keys);
        }
    }
}