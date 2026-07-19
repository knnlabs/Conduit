using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Messaging.Wolverine;

using FluentAssertions;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Moq;

using Wolverine.Logging;
using Wolverine.Persistence.Durability;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Tests for the Wolverine bus health check (I2.8/#931): message-store counts map to
    /// Healthy/Degraded, store failures to Unhealthy.
    /// </summary>
    public class WolverineBusHealthCheckTests
    {
        private readonly Mock<IMessageStore> _storeMock = new();
        private readonly Mock<IMessageStoreAdmin> _adminMock = new();
        private readonly ILogger<WolverineBusHealthCheck> _logger =
            new Mock<ILogger<WolverineBusHealthCheck>>().Object;

        public WolverineBusHealthCheckTests()
        {
            _storeMock.SetupGet(s => s.Admin).Returns(_adminMock.Object);
        }

        private WolverineBusHealthCheck CreateCheck(int threshold = 1) =>
            new(_storeMock.Object, _logger, threshold);

        private static HealthCheckContext Context() => new()
        {
            Registration = new HealthCheckRegistration(
                "wolverine_bus", _ => null!, HealthStatus.Unhealthy, null)
        };

        [Fact]
        public async Task CheckHealthAsync_WithReachableStore_ReturnsHealthyWithCounts()
        {
            _adminMock
                .Setup(a => a.FetchCountsAsync())
                .ReturnsAsync(new PersistedCounts
                {
                    Incoming = 2,
                    Outgoing = 3,
                    Scheduled = 4,
                    DeadLetter = 0,
                    Handled = 5
                });

            var result = await CreateCheck().CheckHealthAsync(Context());

            result.Status.Should().Be(HealthStatus.Healthy);
            result.Data["incoming"].Should().Be(2);
            result.Data["outgoing"].Should().Be(3);
            result.Data["scheduled"].Should().Be(4);
            result.Data["dead_letter"].Should().Be(0);
            result.Data["handled"].Should().Be(5);
        }

        [Fact]
        public async Task CheckHealthAsync_WithDeadLettersAtThreshold_ReturnsDegraded()
        {
            _adminMock
                .Setup(a => a.FetchCountsAsync())
                .ReturnsAsync(new PersistedCounts { DeadLetter = 3 });

            var result = await CreateCheck(threshold: 1).CheckHealthAsync(Context());

            result.Status.Should().Be(HealthStatus.Degraded);
            result.Description.Should().Contain("3");
            result.Data["dead_letter"].Should().Be(3);
        }

        [Fact]
        public async Task CheckHealthAsync_WithDeadLettersBelowThreshold_ReturnsHealthy()
        {
            _adminMock
                .Setup(a => a.FetchCountsAsync())
                .ReturnsAsync(new PersistedCounts { DeadLetter = 2 });

            var result = await CreateCheck(threshold: 5).CheckHealthAsync(Context());

            result.Status.Should().Be(HealthStatus.Healthy);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenStoreThrows_ReturnsUnhealthy()
        {
            _adminMock
                .Setup(a => a.FetchCountsAsync())
                .ThrowsAsync(new InvalidOperationException("connection refused"));

            var result = await CreateCheck().CheckHealthAsync(Context());

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Exception.Should().BeOfType<InvalidOperationException>();
        }

        [Fact]
        public void Constructor_WithNullStore_Throws()
        {
            var act = () => new WolverineBusHealthCheck(null!, _logger);

            act.Should().Throw<ArgumentNullException>().WithParameterName("messageStore");
        }
    }
}
