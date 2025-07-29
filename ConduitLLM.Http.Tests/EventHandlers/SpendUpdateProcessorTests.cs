using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.EventHandlers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Http.Tests.EventHandlers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "EventHandlers")]
    public class SpendUpdateProcessorTests : TestBase
    {
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
        private readonly Mock<IServiceScope> _serviceScopeMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly Mock<IVirtualKeyRepository> _virtualKeyRepositoryMock;
        private readonly SpendUpdateProcessor _processor;

        public SpendUpdateProcessorTests(ITestOutputHelper output) : base(output)
        {
            _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
            _serviceScopeMock = new Mock<IServiceScope>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _virtualKeyRepositoryMock = new Mock<IVirtualKeyRepository>();
            
            // Setup scope factory to return scope
            _serviceScopeFactoryMock.Setup(x => x.CreateScope())
                .Returns(_serviceScopeMock.Object);
            
            // Setup scope to return service provider
            _serviceScopeMock.Setup(x => x.ServiceProvider)
                .Returns(_serviceProviderMock.Object);
            
            var logger = CreateLogger<SpendUpdateProcessor>();
            
            _processor = new SpendUpdateProcessor(
                _serviceScopeFactoryMock.Object,
                _publishEndpointMock.Object,
                logger.Object);
        }

        [Fact]
        public async Task Consume_WithRepositoryAvailable_UpdatesSpendSuccessfully()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);

            var virtualKey = new VirtualKey
            {
                Id = 123,
                KeyHash = "test-hash",
                CurrentSpend = 100m,
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            };

            _virtualKeyRepositoryMock
                .Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(virtualKey));

            _virtualKeyRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 123,
                Amount = 50m,
                RequestId = "req-123",
                CorrelationId = "corr-123"
            };

            var context = new Mock<ConsumeContext<SpendUpdateRequested>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _processor.Consume(context.Object);

            // Assert
            _virtualKeyRepositoryMock.Verify(r => r.UpdateAsync(It.Is<VirtualKey>(vk => 
                vk.CurrentSpend == 150m && 
                vk.Id == 123), It.IsAny<CancellationToken>()), Times.Once);

            _publishEndpointMock.Verify(p => p.Publish(It.Is<SpendUpdated>(su =>
                su.KeyId == 123 &&
                su.Amount == 50m &&
                su.NewTotalSpend == 150m &&
                su.RequestId == "req-123" &&
                su.CorrelationId == "corr-123"), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_WithRepositoryUnavailable_PublishesDeferredEvent()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(null);

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 456,
                Amount = 75m,
                RequestId = "req-456",
                CorrelationId = "corr-456"
            };

            var context = new Mock<ConsumeContext<SpendUpdateRequested>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _processor.Consume(context.Object);

            // Assert
            _publishEndpointMock.Verify(p => p.Publish(It.Is<SpendUpdateDeferred>(sud =>
                sud.KeyId == 456 &&
                sud.Amount == 75m &&
                sud.RequestId == "req-456" &&
                sud.CorrelationId == "corr-456" &&
                sud.Reason == "Repository not available in current context"), 
                It.IsAny<CancellationToken>()), Times.Once);

            _virtualKeyRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Consume_WithZeroAmount_SkipsProcessing()
        {
            // Arrange
            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 789,
                Amount = 0m,
                RequestId = "req-789"
            };

            var context = new Mock<ConsumeContext<SpendUpdateRequested>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _processor.Consume(context.Object);

            // Assert
            _serviceProviderMock.Verify(sp => sp.GetService(It.IsAny<Type>()), Times.Never);
            _publishEndpointMock.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Consume_WithNegativeAmount_SkipsProcessing()
        {
            // Arrange
            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 999,
                Amount = -10m,
                RequestId = "req-999"
            };

            var context = new Mock<ConsumeContext<SpendUpdateRequested>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _processor.Consume(context.Object);

            // Assert
            _serviceProviderMock.Verify(sp => sp.GetService(It.IsAny<Type>()), Times.Never);
            _publishEndpointMock.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Consume_WithNonExistentVirtualKey_SkipsUpdate()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);

            _virtualKeyRepositoryMock
                .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<VirtualKey>(null));

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 111,
                Amount = 25m,
                RequestId = "req-111"
            };

            var context = new Mock<ConsumeContext<SpendUpdateRequested>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            await _processor.Consume(context.Object);

            // Assert
            _virtualKeyRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()), Times.Never);
            _publishEndpointMock.Verify(p => p.Publish(It.IsAny<SpendUpdated>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Consume_WithUpdateFailure_ThrowsException()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);

            var virtualKey = new VirtualKey
            {
                Id = 222,
                KeyHash = "test-hash-222",
                CurrentSpend = 200m
            };

            _virtualKeyRepositoryMock
                .Setup(r => r.GetByIdAsync(222, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(virtualKey));

            _virtualKeyRepositoryMock
                .Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 222,
                Amount = 30m,
                RequestId = "req-222"
            };

            var context = new Mock<ConsumeContext<SpendUpdateRequested>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            var act = () => _processor.Consume(context.Object);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to update spend for virtual key 222");
        }

        [Fact]
        public async Task Consume_WithRepositoryException_ThrowsToTriggerRetry()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);

            _virtualKeyRepositoryMock
                .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Database connection error"));

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 333,
                Amount = 40m,
                RequestId = "req-333"
            };

            var context = new Mock<ConsumeContext<SpendUpdateRequested>>();
            context.Setup(c => c.Message).Returns(@event);

            // Act
            var act = () => _processor.Consume(context.Object);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Database connection error");
        }

        [Fact]
        public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = CreateLogger<SpendUpdateProcessor>();
            
            // Act
            var act = () => new SpendUpdateProcessor(
                null,
                _publishEndpointMock.Object,
                logger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("serviceScopeFactory");
        }

        [Fact]
        public void Constructor_WithNullPublishEndpoint_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = CreateLogger<SpendUpdateProcessor>();
            
            // Act
            var act = () => new SpendUpdateProcessor(
                _serviceScopeFactoryMock.Object,
                null,
                logger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("publishEndpoint");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act
            var act = () => new SpendUpdateProcessor(
                _serviceScopeFactoryMock.Object,
                _publishEndpointMock.Object,
                null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }
    }
}