using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Core.Tests.Services
{
    /// <summary>
    /// Unit tests for EventPublishingServiceBase to ensure standardized event publishing behavior.
    /// </summary>
    public class EventPublishingServiceBaseTests
    {
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<TestEventPublishingService>> _mockLogger;
        private readonly TestEventPublishingService _service;
        private readonly TestEventPublishingService _serviceWithoutEndpoint;

        public EventPublishingServiceBaseTests()
        {
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<TestEventPublishingService>>();
            _service = new TestEventPublishingService(_mockPublishEndpoint.Object, _mockLogger.Object);
            _serviceWithoutEndpoint = new TestEventPublishingService(null, _mockLogger.Object);
        }

        [Fact]
        public async Task PublishEventAsync_WithValidEndpoint_PublishesSuccessfully()
        {
            // Arrange
            var testEvent = new TestEvent { Id = 123, Name = "Test Event" };
            _mockPublishEndpoint.Setup(x => x.Publish(It.IsAny<TestEvent>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.TestPublishEvent(testEvent, "test operation");

            // Assert
            _mockPublishEndpoint.Verify(x => x.Publish(It.Is<TestEvent>(e => e.Id == 123), default), Times.Once);
            _mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Published TestEvent event for test operation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task PublishEventAsync_WithoutEndpoint_LogsDebugAndReturns()
        {
            // Arrange
            var testEvent = new TestEvent { Id = 123, Name = "Test Event" };

            // Act
            await _serviceWithoutEndpoint.TestPublishEvent(testEvent, "test operation");

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Event publishing not configured - skipping TestEvent for test operation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task PublishEventAsync_WithNullEvent_LogsWarningAndReturns()
        {
            // Arrange
            TestEvent? nullEvent = null;

            // Act
            await _service.TestPublishEvent(nullEvent, "test operation");

            // Assert
            _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<TestEvent>(), default), Times.Never);
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempted to publish null event of type TestEvent for test operation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task PublishEventAsync_WhenPublishThrows_LogsWarningAndContinues()
        {
            // Arrange
            var testEvent = new TestEvent { Id = 123, Name = "Test Event" };
            var exception = new Exception("RabbitMQ connection failed");
            _mockPublishEndpoint.Setup(x => x.Publish(It.IsAny<TestEvent>(), default))
                .ThrowsAsync(exception);

            // Act
            await _service.TestPublishEvent(testEvent, "test operation");

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to publish TestEvent event for test operation - operation completed but event not sent")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task PublishEventAsync_WithContextData_IncludesContextInLogs()
        {
            // Arrange
            var testEvent = new TestEvent { Id = 123, Name = "Test Event" };
            var contextData = new { UserId = 456, Action = "Update" };
            _mockPublishEndpoint.Setup(x => x.Publish(It.IsAny<TestEvent>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.TestPublishEventWithContext(testEvent, "test operation", contextData);

            // Assert
            _mockPublishEndpoint.Verify(x => x.Publish(It.Is<TestEvent>(e => e.Id == 123), default), Times.Once);
            _mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Published TestEvent event for test operation with context")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public void IsEventPublishingEnabled_WithEndpoint_ReturnsTrue()
        {
            // Assert
            Assert.True(_service.TestIsEventPublishingEnabled);
        }

        [Fact]
        public void IsEventPublishingEnabled_WithoutEndpoint_ReturnsFalse()
        {
            // Assert
            Assert.False(_serviceWithoutEndpoint.TestIsEventPublishingEnabled);
        }

        [Fact]
        public void LogEventPublishingConfiguration_WithEndpoint_LogsEnabledMessage()
        {
            // Act
            _service.TestLogConfiguration("TestService");

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("TestService: Event bus configured - using event-driven architecture")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public void LogEventPublishingConfiguration_WithoutEndpoint_LogsWarningMessage()
        {
            // Act
            _serviceWithoutEndpoint.TestLogConfiguration("TestService");

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("TestService: Event bus NOT configured - using direct database updates (not recommended for production)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        // Test event class
        private class TestEvent
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        // Test service implementation
        public class TestEventPublishingService : EventPublishingServiceBase
        {
            public TestEventPublishingService(IPublishEndpoint? publishEndpoint, ILogger<TestEventPublishingService> logger)
                : base(publishEndpoint, logger)
            {
            }

            public async Task TestPublishEvent<TEvent>(TEvent domainEvent, string operationName) where TEvent : class
            {
                await PublishEventAsync(domainEvent, operationName);
            }

            public async Task TestPublishEventWithContext<TEvent>(TEvent domainEvent, string operationName, object contextData) where TEvent : class
            {
                await PublishEventAsync(domainEvent, operationName, contextData);
            }

            public bool TestIsEventPublishingEnabled => IsEventPublishingEnabled;

            public void TestLogConfiguration(string serviceName)
            {
                LogEventPublishingConfiguration(serviceName);
            }
        }
    }
}