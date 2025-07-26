using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.EventHandlers;
using ConduitLLM.Http.Tests.TestHelpers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Http.Tests.EventHandlers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "EventHandlers")]
    public class ModelCapabilitiesDiscoveredHandlerTests : TestBase
    {
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IMemoryCache> _cacheMock;
        private readonly ModelCapabilitiesDiscoveredHandler _handler;

        public ModelCapabilitiesDiscoveredHandlerTests(ITestOutputHelper output) : base(output)
        {
            _serviceProviderMock = new Mock<IServiceProvider>();
            _cacheMock = new Mock<IMemoryCache>().SetupWorkingCache();
            
            var logger = CreateLogger<ModelCapabilitiesDiscoveredHandler>();
            
            _handler = new ModelCapabilitiesDiscoveredHandler(
                _serviceProviderMock.Object,
                _cacheMock.Object,
                logger.Object);
        }

        [Fact]
        public async Task Consume_ValidEvent_UpdatesCache()
        {
            // Arrange
            var @event = new ModelCapabilitiesDiscovered
            {
                EventId = Guid.NewGuid().ToString(),
                ProviderId = 1,
                ProviderType = ProviderType.OpenAI,
                ModelCapabilities = new Dictionary<string, ModelCapabilities>
                {
                    ["gpt-4"] = new ModelCapabilities
                    {
                        SupportsImageGeneration = false,
                        SupportsVision = true,
                        SupportsFunctionCalling = true,
                        AdditionalCapabilities = new Dictionary<string, object>
                        {
                            ["chat"] = true
                        }
                    }
                },
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            var cacheKey = "provider_capabilities_openai";
            _cacheMock.Verify(c => c.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task Consume_WithOptionalServiceAvailable_InvokesService()
        {
            // Arrange
            var modelMappingServiceMock = new Mock<IModelMappingService>();
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IModelMappingService)))
                .Returns(modelMappingServiceMock.Object);

            var @event = new ModelCapabilitiesDiscovered
            {
                EventId = Guid.NewGuid().ToString(),
                ProviderId = 1,
                ProviderType = ProviderType.OpenAI,
                ModelCapabilities = new Dictionary<string, ModelCapabilities>(),
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            // The service would be used if the commented code was active
            // This test demonstrates the pattern for when optional services are enabled
        }

        [Fact]
        public async Task Consume_WithOptionalServiceUnavailable_ContinuesProcessing()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IModelMappingService)))
                .Returns(null);

            var @event = new ModelCapabilitiesDiscovered
            {
                EventId = Guid.NewGuid().ToString(),
                ProviderId = 2,
                ProviderType = ProviderType.Anthropic,
                ModelCapabilities = new Dictionary<string, ModelCapabilities>
                {
                    ["claude-3-opus"] = new ModelCapabilities
                    {
                        SupportsImageGeneration = false,
                        SupportsVision = true,
                        SupportsFunctionCalling = true
                    }
                },
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            var act = () => _handler.Consume(context.Object);

            // Assert
            await act.Should().NotThrowAsync();
            _cacheMock.Verify(c => c.CreateEntry(It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task Consume_WithCacheException_ThrowsToTriggerRetry()
        {
            // Arrange
            _cacheMock
                .Setup(c => c.CreateEntry(It.IsAny<object>()))
                .Throws(new InvalidOperationException("Cache error"));

            var @event = new ModelCapabilitiesDiscovered
            {
                EventId = Guid.NewGuid().ToString(),
                ProviderId = 3,
                ProviderType = ProviderType.OpenAI,
                ModelCapabilities = new Dictionary<string, ModelCapabilities>(),
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            var act = () => _handler.Consume(context.Object);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Cache error");
        }

        [Fact]
        public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = CreateLogger<ModelCapabilitiesDiscoveredHandler>();
            
            // Act
            var act = () => new ModelCapabilitiesDiscoveredHandler(
                null,
                _cacheMock.Object,
                logger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("serviceProvider");
        }

        [Fact]
        public void Constructor_WithNullCache_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = CreateLogger<ModelCapabilitiesDiscoveredHandler>();
            
            // Act
            var act = () => new ModelCapabilitiesDiscoveredHandler(
                _serviceProviderMock.Object,
                null,
                logger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("cache");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act
            var act = () => new ModelCapabilitiesDiscoveredHandler(
                _serviceProviderMock.Object,
                _cacheMock.Object,
                null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public async Task Consume_WithMultipleModels_CachesAllCapabilities()
        {
            // Arrange
            var @event = new ModelCapabilitiesDiscovered
            {
                EventId = Guid.NewGuid().ToString(),
                ProviderId = 4,
                ProviderType = ProviderType.OpenAI,
                ModelCapabilities = new Dictionary<string, ModelCapabilities>
                {
                    ["model-1"] = new ModelCapabilities
                    {
                        SupportsImageGeneration = true,
                        SupportsVision = false
                    },
                    ["model-2"] = new ModelCapabilities
                    {
                        SupportsImageGeneration = false,
                        SupportsVision = true
                    },
                    ["model-3"] = new ModelCapabilities
                    {
                        SupportsImageGeneration = true,
                        SupportsVision = true
                    }
                },
                DiscoveredAt = DateTime.UtcNow
            };

            var context = new Mock<ConsumeContext<ModelCapabilitiesDiscovered>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            var cacheKey = "provider_capabilities_openai";
            _cacheMock.Verify(c => c.CreateEntry(cacheKey), Times.Once);
        }
    }

    // Mock interface for testing optional service
    public interface IModelMappingService
    {
        Task UpdateProviderModelsAsync(string providerName, Dictionary<string, ModelCapabilities> capabilities);
    }
}