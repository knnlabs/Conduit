using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.Consumers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Consumers
{
    [Trait("Category", "Unit")]
    public class LLMCacheToggleConsumerTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<LLMCacheToggleConsumer>> _mockLogger;
        private readonly LLMCacheToggleConsumer _consumer;
        private string? _capturedConfigValue;

        public LLMCacheToggleConsumerTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<LLMCacheToggleConsumer>>();

            // Capture the value set to IConfiguration
            _mockConfiguration
                .SetupSet(x => x["Cache:LLMCachingEnabled"] = It.IsAny<string>())
                .Callback<string, string>((key, value) => _capturedConfigValue = value);

            _consumer = new LLMCacheToggleConsumer(
                _mockConfiguration.Object,
                _mockLogger.Object);
        }

        private Mock<ConsumeContext<LLMCacheToggleEvent>> CreateMockContext(LLMCacheToggleEvent @event)
        {
            var mockContext = new Mock<ConsumeContext<LLMCacheToggleEvent>>();
            mockContext.Setup(x => x.Message).Returns(@event);
            return mockContext;
        }

        [Fact]
        public async Task Consume_EnableCache_UpdatesConfigurationToTrue()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = true,
                ToggledBy = "admin",
                ToggledAt = DateTime.UtcNow,
                Reason = "Testing feature",
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            Assert.Equal("True", _capturedConfigValue);
            _mockConfiguration.VerifySet(x => x["Cache:LLMCachingEnabled"] = "True", Times.Once);
        }

        [Fact]
        public async Task Consume_DisableCache_UpdatesConfigurationToFalse()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = false,
                ToggledBy = "admin",
                ToggledAt = DateTime.UtcNow,
                Reason = "Performance issues",
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            Assert.Equal("False", _capturedConfigValue);
            _mockConfiguration.VerifySet(x => x["Cache:LLMCachingEnabled"] = "False", Times.Once);
        }

        [Fact]
        public async Task Consume_EnableCache_LogsWarningWithCorrectMessage()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = true,
                ToggledBy = "admin-user",
                ToggledAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                Reason = "Testing caching",
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("ENABLED")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_DisableCache_LogsWarningWithCorrectMessage()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = false,
                ToggledBy = "admin-user",
                ToggledAt = DateTime.UtcNow,
                Reason = "Rollback due to errors",
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("DISABLED")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_LogsToggledByInformation()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = true,
                ToggledBy = "john.doe@example.com",
                ToggledAt = DateTime.UtcNow,
                Reason = "Production deployment",
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("john.doe@example.com")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_LogsReasonWhenProvided()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = true,
                ToggledBy = "admin",
                ToggledAt = DateTime.UtcNow,
                Reason = "Enabling for performance testing",
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Enabling for performance testing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_LogsNoneProvidedWhenReasonIsNull()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = true,
                ToggledBy = "admin",
                ToggledAt = DateTime.UtcNow,
                Reason = null,
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            await _consumer.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("None provided")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_CompletesSuccessfully()
        {
            // Arrange
            var @event = new LLMCacheToggleEvent
            {
                Enabled = true,
                ToggledBy = "admin",
                ToggledAt = DateTime.UtcNow,
                ApplyImmediately = true
            };

            var mockContext = CreateMockContext(@event);

            // Act
            var task = _consumer.Consume(mockContext.Object);
            await task;

            // Assert
            Assert.True(task.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task Consume_MultipleToggles_UpdatesConfigurationEachTime()
        {
            // Arrange
            var enableEvent = new LLMCacheToggleEvent
            {
                Enabled = true,
                ToggledBy = "admin",
                ToggledAt = DateTime.UtcNow
            };

            var disableEvent = new LLMCacheToggleEvent
            {
                Enabled = false,
                ToggledBy = "admin",
                ToggledAt = DateTime.UtcNow.AddMinutes(5)
            };

            var enableContext = CreateMockContext(enableEvent);
            var disableContext = CreateMockContext(disableEvent);

            // Act
            await _consumer.Consume(enableContext.Object);
            Assert.Equal("True", _capturedConfigValue);

            await _consumer.Consume(disableContext.Object);
            Assert.Equal("False", _capturedConfigValue);

            // Assert
            _mockConfiguration.VerifySet(x => x["Cache:LLMCachingEnabled"] = It.IsAny<string>(), Times.Exactly(2));
        }
    }
}
