using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.EventHandlers;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Http.Tests.EventHandlers
{
    public class ModelCapabilitiesDiscoveredHandlerTests
    {
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ILogger<ModelCapabilitiesDiscoveredHandler>> _mockLogger;
        private readonly ModelCapabilitiesDiscoveredHandler _handler;

        public ModelCapabilitiesDiscoveredHandlerTests()
        {
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger<ModelCapabilitiesDiscoveredHandler>>();
            _handler = new ModelCapabilitiesDiscoveredHandler(_mockCache.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Consume_ShouldCacheModelCapabilities_WhenEventReceived()
        {
            // Arrange
            var modelCapabilities = new Dictionary<string, ModelCapabilities>
            {
                ["gpt-4"] = new ModelCapabilities
                {
                    SupportsImageGeneration = false,
                    SupportsVision = true,
                    SupportsFunctionCalling = true,
                    AdditionalCapabilities = new Dictionary<string, object>
                    {
                        ["chat"] = true,
                        ["maxTokens"] = 8192
                    }
                },
                ["dall-e-3"] = new ModelCapabilities
                {
                    SupportsImageGeneration = true,
                    SupportsVision = false,
                    SupportsFunctionCalling = false,
                    AdditionalCapabilities = new Dictionary<string, object>()
                }
            };

            var @event = new ModelCapabilitiesDiscovered
            {
                ProviderId = 1,
                ProviderName = "openai",
                ModelCapabilities = modelCapabilities,
                DiscoveredAt = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(x => x.Message).Returns(@event);

            object cacheEntry = null;
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>()).Callback<object>(value => cacheEntry = value);
            mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
            
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(x => x.CreateEntry("provider_capabilities_openai"), Times.Once);
            mockCacheEntry.VerifySet(x => x.Value = It.IsAny<Dictionary<string, ModelCapabilities>>(), Times.Once);
            mockCacheEntry.VerifySet(x => x.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24), Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldLogModelCapabilities_WhenEventReceived()
        {
            // Arrange
            var modelCapabilities = new Dictionary<string, ModelCapabilities>
            {
                ["test-model"] = new ModelCapabilities
                {
                    SupportsImageGeneration = true,
                    SupportsVision = true,
                    AdditionalCapabilities = new Dictionary<string, object> { ["chat"] = true }
                }
            };

            var @event = new ModelCapabilitiesDiscovered
            {
                ProviderId = 1,
                ProviderName = "testprovider",
                ModelCapabilities = modelCapabilities,
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(x => x.Message).Returns(@event);

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing ModelCapabilitiesDiscovered event")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldLogMetrics_WhenMultipleModelsDiscovered()
        {
            // Arrange
            var modelCapabilities = new Dictionary<string, ModelCapabilities>
            {
                ["chat-model"] = new ModelCapabilities
                {
                    AdditionalCapabilities = new Dictionary<string, object> { ["chat"] = true }
                },
                ["image-model"] = new ModelCapabilities
                {
                    SupportsImageGeneration = true
                },
                ["vision-model"] = new ModelCapabilities
                {
                    SupportsVision = true,
                    AdditionalCapabilities = new Dictionary<string, object> { ["chat"] = true }
                }
            };

            var @event = new ModelCapabilitiesDiscovered
            {
                ProviderId = 1,
                ProviderName = "testprovider",
                ModelCapabilities = modelCapabilities,
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(x => x.Message).Returns(@event);

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("discovery metrics")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldThrowAndLogError_WhenExceptionOccurs()
        {
            // Arrange
            var @event = new ModelCapabilitiesDiscovered
            {
                ProviderId = 1,
                ProviderName = "testprovider",
                ModelCapabilities = new Dictionary<string, ModelCapabilities>(),
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(x => x.Message).Returns(@event);

            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Throws(new InvalidOperationException("Cache error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Consume(context.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing ModelCapabilitiesDiscovered")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}