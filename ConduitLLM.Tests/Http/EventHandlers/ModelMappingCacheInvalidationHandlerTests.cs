using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.EventHandlers;
using ConduitLLM.Http.Interfaces;

namespace ConduitLLM.Tests.Http.EventHandlers
{
    [Trait("Category", "Unit")]
    public class ModelMappingCacheInvalidationHandlerTests
    {
        private readonly Mock<ISettingsRefreshService> _mockSettingsRefreshService;
        private readonly Mock<IDiscoveryCacheService> _mockDiscoveryCacheService;
        private readonly Mock<ILogger<ModelMappingCacheInvalidationHandler>> _mockLogger;
        private readonly ModelMappingCacheInvalidationHandler _handler;

        public ModelMappingCacheInvalidationHandlerTests()
        {
            _mockSettingsRefreshService = new Mock<ISettingsRefreshService>();
            _mockDiscoveryCacheService = new Mock<IDiscoveryCacheService>();
            _mockLogger = new Mock<ILogger<ModelMappingCacheInvalidationHandler>>();

            _handler = new ModelMappingCacheInvalidationHandler(
                _mockSettingsRefreshService.Object,
                _mockDiscoveryCacheService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task Consume_Should_Refresh_Settings_And_Invalidate_Discovery_Cache_On_Created()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 123,
                ModelAlias = "gpt-4-turbo",
                ProviderId = 1,
                IsEnabled = true,
                ChangeType = "Created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var mockContext = new Mock<ConsumeContext<ModelMappingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);

            // Act
            await _handler.Consume(mockContext.Object);

            // Assert
            _mockSettingsRefreshService.Verify(
                x => x.RefreshModelMappingsAsync(), 
                Times.Once);
            
            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Refresh_Settings_And_Invalidate_Discovery_Cache_On_Updated()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 456,
                ModelAlias = "claude-3-opus",
                ProviderId = 2,
                IsEnabled = false,
                ChangeType = "Updated",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var mockContext = new Mock<ConsumeContext<ModelMappingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);

            // Act
            await _handler.Consume(mockContext.Object);

            // Assert
            _mockSettingsRefreshService.Verify(
                x => x.RefreshModelMappingsAsync(), 
                Times.Once);
            
            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Refresh_Settings_And_Invalidate_Discovery_Cache_On_Deleted()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 789,
                ModelAlias = "gemini-pro",
                ProviderId = 3,
                IsEnabled = true,
                ChangeType = "Deleted",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var mockContext = new Mock<ConsumeContext<ModelMappingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);

            // Act
            await _handler.Consume(mockContext.Object);

            // Assert
            _mockSettingsRefreshService.Verify(
                x => x.RefreshModelMappingsAsync(), 
                Times.Once);
            
            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Log_Information_On_Success()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 999,
                ModelAlias = "test-model",
                ProviderId = 4,
                IsEnabled = true,
                ChangeType = "Created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var mockContext = new Mock<ConsumeContext<ModelMappingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);

            // Act
            await _handler.Consume(mockContext.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing ModelMappingChanged event")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Successfully refreshed model mappings")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Throw_And_Log_Error_When_RefreshSettings_Fails()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 111,
                ModelAlias = "error-model",
                ProviderId = 5,
                IsEnabled = true,
                ChangeType = "Updated",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var mockContext = new Mock<ConsumeContext<ModelMappingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);

            var expectedException = new InvalidOperationException("Database connection failed");
            _mockSettingsRefreshService
                .Setup(x => x.RefreshModelMappingsAsync())
                .ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _handler.Consume(mockContext.Object));

            Assert.Equal(expectedException.Message, actualException.Message);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to refresh model mappings")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            // Verify that cache invalidation was not called due to early failure
            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), 
                Times.Never);
        }

        [Fact]
        public async Task Consume_Should_Throw_And_Log_Error_When_CacheInvalidation_Fails()
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 222,
                ModelAlias = "cache-error-model",
                ProviderId = 6,
                IsEnabled = true,
                ChangeType = "Created",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var mockContext = new Mock<ConsumeContext<ModelMappingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);

            var expectedException = new InvalidOperationException("Redis connection failed");
            _mockDiscoveryCacheService
                .Setup(x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _handler.Consume(mockContext.Object));

            Assert.Equal(expectedException.Message, actualException.Message);

            // Verify settings refresh was called before the failure
            _mockSettingsRefreshService.Verify(
                x => x.RefreshModelMappingsAsync(), 
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to refresh model mappings or invalidate cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("Created")]
        [InlineData("Updated")]
        [InlineData("Deleted")]
        public async Task Consume_Should_Handle_All_Change_Types(string changeType)
        {
            // Arrange
            var @event = new ModelMappingChanged
            {
                MappingId = 333,
                ModelAlias = $"model-{changeType.ToLower()}",
                ProviderId = 7,
                IsEnabled = true,
                ChangeType = changeType,
                CorrelationId = Guid.NewGuid().ToString()
            };

            var mockContext = new Mock<ConsumeContext<ModelMappingChanged>>();
            mockContext.Setup(x => x.Message).Returns(@event);

            // Act
            await _handler.Consume(mockContext.Object);

            // Assert
            _mockSettingsRefreshService.Verify(
                x => x.RefreshModelMappingsAsync(), 
                Times.Once);
            
            _mockDiscoveryCacheService.Verify(
                x => x.InvalidateAllDiscoveryAsync(It.IsAny<CancellationToken>()), 
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Invalidated all discovery cache entries after {changeType}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_SettingsRefreshService()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelMappingCacheInvalidationHandler(
                    null,
                    _mockDiscoveryCacheService.Object,
                    _mockLogger.Object));
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_DiscoveryCacheService()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelMappingCacheInvalidationHandler(
                    _mockSettingsRefreshService.Object,
                    null,
                    _mockLogger.Object));
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Logger()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelMappingCacheInvalidationHandler(
                    _mockSettingsRefreshService.Object,
                    _mockDiscoveryCacheService.Object,
                    null));
        }
    }
}