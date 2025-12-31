using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Consumers;
using ConduitLLM.Core.Events;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Consumers
{
    /// <summary>
    /// Unit tests for GlobalSettingCacheInvalidationHandler consumer
    /// Ensures event-driven cache synchronization works correctly across distributed instances
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Consumer")]
    public class GlobalSettingCacheInvalidationHandlerTests
    {
        private readonly Mock<IGlobalSettingsCacheService> _mockCacheService;
        private readonly Mock<ILogger<GlobalSettingCacheInvalidationHandler>> _mockLogger;
        private readonly GlobalSettingCacheInvalidationHandler _consumer;

        public GlobalSettingCacheInvalidationHandlerTests()
        {
            _mockCacheService = new Mock<IGlobalSettingsCacheService>();
            _mockLogger = new Mock<ILogger<GlobalSettingCacheInvalidationHandler>>();
            _consumer = new GlobalSettingCacheInvalidationHandler(
                _mockCacheService.Object,
                _mockLogger.Object);
        }

        private Mock<ConsumeContext<GlobalSettingChanged>> CreateMockContext(GlobalSettingChanged @event)
        {
            var mockContext = new Mock<ConsumeContext<GlobalSettingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);
            return mockContext;
        }

        #region Basic Consumption Tests

        [Fact]
        public async Task Consume_WithValidEvent_CallsInvalidateSettingAsync()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "max_agentic_iterations",
                ChangeType = "Updated",
                ChangedProperties = new[] { "Value" }
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockCacheService.Verify(
                x => x.InvalidateSettingAsync("max_agentic_iterations"),
                Times.Once);
        }

        [Fact]
        public async Task Consume_WithValidEvent_LogsInformationAboutReceivedEvent()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 5,
                SettingKey = "rate_limit",
                ChangeType = "Created"
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("Received GlobalSettingChanged event") &&
                        o.ToString()!.Contains("rate_limit") &&
                        o.ToString()!.Contains("5")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_AfterSuccessfulInvalidation_LogsSuccessMessage()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "test_setting",
                ChangeType = "Updated"
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("Successfully invalidated cache") &&
                        o.ToString()!.Contains("test_setting")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Different Change Types

        [Theory]
        [InlineData("Created")]
        [InlineData("Updated")]
        [InlineData("Deleted")]
        public async Task Consume_WithDifferentChangeTypes_InvalidatesCache(string changeType)
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 10,
                SettingKey = "dynamic_setting",
                ChangeType = changeType
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockCacheService.Verify(
                x => x.InvalidateSettingAsync("dynamic_setting"),
                Times.Once);
        }

        [Fact]
        public async Task Consume_WithCreatedChangeType_LogsCorrectChangeType()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "new_setting",
                ChangeType = "Created"
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("ChangeType: Created")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Consume_WhenInvalidationFails_LogsError()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "failing_setting",
                ChangeType = "Updated"
            };

            var mockContext = CreateMockContext(@event);

            var exception = new InvalidOperationException("Cache service unavailable");
            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _consumer.Consume(mockContext.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("Failed to invalidate cache") &&
                        o.ToString()!.Contains("failing_setting")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_WhenInvalidationFails_RethrowsExceptionForMassTransitRetry()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "error_setting",
                ChangeType = "Updated"
            };

            var mockContext = CreateMockContext(@event);

            var exception = new InvalidOperationException("Database connection lost");
            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .ThrowsAsync(exception);

            // Act & Assert
            var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _consumer.Consume(mockContext.Object));

            thrownException.Should().Be(exception);
        }

        [Fact]
        public async Task Consume_WhenInvalidationThrowsNullReferenceException_StillRethrows()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "null_setting",
                ChangeType = "Updated"
            };

            var mockContext = CreateMockContext(@event);

            var exception = new NullReferenceException("Cache service is null");
            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(
                async () => await _consumer.Consume(mockContext.Object));
        }

        #endregion

        #region Concurrent Operations Tests

        [Fact]
        public async Task Consume_MultipleEventsForSameSetting_InvalidatesCacheMultipleTimes()
        {
            // Arrange
            var event1 = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "concurrent_setting",
                ChangeType = "Updated"
            };

            var event2 = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "concurrent_setting",
                ChangeType = "Updated"
            };

            var mockContext1 = CreateMockContext(event1);
            var mockContext2 = CreateMockContext(event2);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext1.Object);
            await _consumer.Consume(mockContext2.Object);

            // Assert
            _mockCacheService.Verify(
                x => x.InvalidateSettingAsync("concurrent_setting"),
                Times.Exactly(2));
        }

        [Fact]
        public async Task Consume_MultipleEventsForDifferentSettings_InvalidatesEachSettingIndependently()
        {
            // Arrange
            var event1 = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "setting_a",
                ChangeType = "Updated"
            };

            var event2 = new GlobalSettingChanged
            {
                SettingId = 2,
                SettingKey = "setting_b",
                ChangeType = "Created"
            };

            var mockContext1 = CreateMockContext(event1);
            var mockContext2 = CreateMockContext(event2);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext1.Object);
            await _consumer.Consume(mockContext2.Object);

            // Assert
            _mockCacheService.Verify(x => x.InvalidateSettingAsync("setting_a"), Times.Once);
            _mockCacheService.Verify(x => x.InvalidateSettingAsync("setting_b"), Times.Once);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task Consume_WithEmptySettingKey_StillCallsInvalidate()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "",
                ChangeType = "Updated"
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockCacheService.Verify(
                x => x.InvalidateSettingAsync(""),
                Times.Once);
        }

        [Fact]
        public async Task Consume_WithNullChangedProperties_ProcessesSuccessfully()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "test_setting",
                ChangeType = "Updated",
                ChangedProperties = Array.Empty<string>()
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockCacheService.Verify(
                x => x.InvalidateSettingAsync("test_setting"),
                Times.Once);
        }

        [Fact]
        public async Task Consume_CompletesSuccessfully()
        {
            // Arrange
            var @event = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "completion_test",
                ChangeType = "Updated"
            };

            var mockContext = CreateMockContext(@event);

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var task = _consumer.Consume(mockContext.Object);
            await task;

            // Assert
            task.IsCompletedSuccessfully.Should().BeTrue();
        }

        #endregion

        #region Integration Scenario Tests

        [Fact]
        public async Task Consume_SimulatingFullCreateUpdateDeleteCycle_HandlesAllCorrectly()
        {
            // Arrange
            var createEvent = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "lifecycle_setting",
                ChangeType = "Created"
            };

            var updateEvent = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "lifecycle_setting",
                ChangeType = "Updated"
            };

            var deleteEvent = new GlobalSettingChanged
            {
                SettingId = 1,
                SettingKey = "lifecycle_setting",
                ChangeType = "Deleted"
            };

            _mockCacheService
                .Setup(x => x.InvalidateSettingAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _consumer.Consume(CreateMockContext(createEvent).Object);
            await _consumer.Consume(CreateMockContext(updateEvent).Object);
            await _consumer.Consume(CreateMockContext(deleteEvent).Object);

            // Assert
            _mockCacheService.Verify(
                x => x.InvalidateSettingAsync("lifecycle_setting"),
                Times.Exactly(3));
        }

        #endregion
    }
}
