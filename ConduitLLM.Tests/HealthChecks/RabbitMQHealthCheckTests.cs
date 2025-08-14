using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.HealthChecks;

namespace ConduitLLM.Tests.HealthChecks
{
    public class RabbitMQHealthCheckTests
    {
        private readonly Mock<IBus> _busMock;
        private readonly Mock<ILogger<RabbitMQHealthCheck>> _loggerMock;
        private readonly RabbitMQHealthCheck _healthCheck;

        public RabbitMQHealthCheckTests()
        {
            _busMock = new Mock<IBus>();
            _loggerMock = new Mock<ILogger<RabbitMQHealthCheck>>();

            _healthCheck = new RabbitMQHealthCheck(
                _busMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenBusIsInjected_ShouldReturnHealthy()
        {
            // Arrange
            var context = new HealthCheckContext 
            { 
                Registration = new HealthCheckRegistration("test", _ => _healthCheck, null, null) 
            };

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("RabbitMQ connected", result.Description);
        }

    }
}