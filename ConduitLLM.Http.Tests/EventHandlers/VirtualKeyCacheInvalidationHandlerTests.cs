using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.EventHandlers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Http.Tests.EventHandlers
{
    /// <summary>
    /// Tests for VirtualKeyCacheInvalidationHandler
    /// </summary>
    public class VirtualKeyCacheInvalidationHandlerTests
    {
        private readonly Mock<IVirtualKeyCache> _mockCache;
        private readonly Mock<ILogger<VirtualKeyCacheInvalidationHandler>> _mockLogger;
        private readonly VirtualKeyCacheInvalidationHandler _handler;

        public VirtualKeyCacheInvalidationHandlerTests()
        {
            _mockCache = new Mock<IVirtualKeyCache>();
            _mockLogger = new Mock<ILogger<VirtualKeyCacheInvalidationHandler>>();
            _handler = new VirtualKeyCacheInvalidationHandler(_mockCache.Object, _mockLogger.Object);
        }

        #region VirtualKeyUpdated Tests

        [Fact]
        public async Task Consume_VirtualKeyUpdated_InvalidatesCache()
        {
            // Arrange
            var @event = new VirtualKeyUpdated
            {
                KeyId = 123,
                KeyHash = "test-hash",
                ChangedProperties = new[] { "AllowedModels", "MaxBudget" },
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<VirtualKeyUpdated>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(c => c.InvalidateVirtualKeyAsync(@event.KeyHash), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Virtual Key cache invalidated for key 123")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_VirtualKeyUpdated_WithCreatedMarker_InvalidatesCache()
        {
            // Arrange
            var @event = new VirtualKeyUpdated
            {
                KeyId = 456,
                KeyHash = "new-key-hash",
                ChangedProperties = new[] { "Created" }, // Special marker for new keys
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<VirtualKeyUpdated>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(c => c.InvalidateVirtualKeyAsync(@event.KeyHash), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("properties changed: Created")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_VirtualKeyUpdated_NullCache_SkipsInvalidation()
        {
            // Arrange
            var handler = new VirtualKeyCacheInvalidationHandler(null, _mockLogger.Object);
            var @event = new VirtualKeyUpdated
            {
                KeyId = 789,
                KeyHash = "test-hash",
                ChangedProperties = new[] { "IsEnabled" },
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<VirtualKeyUpdated>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Virtual Key cache not configured")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_VirtualKeyUpdated_CacheThrows_LogsErrorAndRethrows()
        {
            // Arrange
            var @event = new VirtualKeyUpdated
            {
                KeyId = 999,
                KeyHash = "error-hash",
                ChangedProperties = new[] { "MaxBudget" },
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<VirtualKeyUpdated>>();
            context.Setup(c => c.Message).Returns(@event);

            var exception = new InvalidOperationException("Cache error");
            _mockCache.Setup(c => c.InvalidateVirtualKeyAsync(It.IsAny<string>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Consume(context.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate Virtual Key cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region VirtualKeyDeleted Tests

        [Fact]
        public async Task Consume_VirtualKeyDeleted_InvalidatesCache()
        {
            // Arrange
            var @event = new VirtualKeyDeleted
            {
                KeyId = 321,
                KeyHash = "deleted-hash",
                KeyName = "Test Key",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<VirtualKeyDeleted>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(c => c.InvalidateVirtualKeyAsync(@event.KeyHash), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Virtual Key cache invalidated for deleted key 321")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_VirtualKeyDeleted_NullCache_SkipsInvalidation()
        {
            // Arrange
            var handler = new VirtualKeyCacheInvalidationHandler(null, _mockLogger.Object);
            var @event = new VirtualKeyDeleted
            {
                KeyId = 654,
                KeyHash = "deleted-hash",
                KeyName = "Test Key",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<VirtualKeyDeleted>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Virtual Key cache not configured")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_VirtualKeyDeleted_CacheThrows_LogsErrorAndRethrows()
        {
            // Arrange
            var @event = new VirtualKeyDeleted
            {
                KeyId = 888,
                KeyHash = "error-hash",
                KeyName = "Error Key",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<VirtualKeyDeleted>>();
            context.Setup(c => c.Message).Returns(@event);

            var exception = new InvalidOperationException("Cache error");
            _mockCache.Setup(c => c.InvalidateVirtualKeyAsync(It.IsAny<string>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Consume(context.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate Virtual Key cache for deleted key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region SpendUpdated Tests

        [Fact]
        public async Task Consume_SpendUpdated_InvalidatesCache()
        {
            // Arrange
            var @event = new SpendUpdated
            {
                KeyId = 111,
                KeyHash = "spend-hash",
                Amount = 10.50m,
                NewTotalSpend = 150.75m,
                RequestId = "req-123",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<SpendUpdated>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(c => c.InvalidateVirtualKeyAsync(@event.KeyHash), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("new total: 150.75")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_SpendUpdated_NullCache_SkipsInvalidation()
        {
            // Arrange
            var handler = new VirtualKeyCacheInvalidationHandler(null, _mockLogger.Object);
            var @event = new SpendUpdated
            {
                KeyId = 222,
                KeyHash = "spend-hash",
                Amount = 5.25m,
                NewTotalSpend = 75.50m,
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<SpendUpdated>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Virtual Key cache not configured")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_SpendUpdated_CacheThrows_LogsErrorAndRethrows()
        {
            // Arrange
            var @event = new SpendUpdated
            {
                KeyId = 333,
                KeyHash = "error-hash",
                Amount = 20.00m,
                NewTotalSpend = 200.00m,
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<SpendUpdated>>();
            context.Setup(c => c.Message).Returns(@event);

            var exception = new InvalidOperationException("Cache error");
            _mockCache.Setup(c => c.InvalidateVirtualKeyAsync(It.IsAny<string>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Consume(context.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate Virtual Key cache after spend update")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Partition Key Tests

        [Fact]
        public void VirtualKeyEvents_HaveCorrectPartitionKeys()
        {
            // Test that all virtual key events use KeyId as partition key
            var updateEvent = new VirtualKeyUpdated { KeyId = 123 };
            var deleteEvent = new VirtualKeyDeleted { KeyId = 456 };
            var spendEvent = new SpendUpdated { KeyId = 789 };

            Assert.Equal("123", updateEvent.PartitionKey);
            Assert.Equal("456", deleteEvent.PartitionKey);
            Assert.Equal("789", spendEvent.PartitionKey);
        }

        #endregion
    }
}