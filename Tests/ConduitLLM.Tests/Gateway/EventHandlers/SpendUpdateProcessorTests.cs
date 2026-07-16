using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Gateway.EventHandlers;
using ConduitLLM.Tests.Messaging;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.EventHandlers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "EventHandlers")]
    public class SpendUpdateProcessorTests : TestBase
    {
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
        private readonly Mock<IServiceScope> _serviceScopeMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IEventBus> _eventBusMock;
        private readonly Mock<IVirtualKeyRepository> _virtualKeyRepositoryMock;
        private readonly Mock<IVirtualKeyGroupRepository> _groupRepositoryMock;
        private readonly SpendUpdateProcessor _processor;

        public SpendUpdateProcessorTests(ITestOutputHelper output) : base(output)
        {
            _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
            _serviceScopeMock = new Mock<IServiceScope>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _eventBusMock = new Mock<IEventBus>();
            _virtualKeyRepositoryMock = new Mock<IVirtualKeyRepository>();
            _groupRepositoryMock = new Mock<IVirtualKeyGroupRepository>();
            
            // Setup scope factory to return scope
            _serviceScopeFactoryMock.Setup(x => x.CreateScope())
                .Returns(_serviceScopeMock.Object);
            
            // Setup scope to return service provider
            _serviceScopeMock.Setup(x => x.ServiceProvider)
                .Returns(_serviceProviderMock.Object);
            
            var logger = CreateLogger<SpendUpdateProcessor>();
            
            _processor = new SpendUpdateProcessor(
                _serviceScopeFactoryMock.Object,
                _eventBusMock.Object,
                logger.Object);
        }

        [Fact]
        public async Task HandleAsync_WithRepositoryAvailable_UpdatesSpendSuccessfully()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyGroupRepository)))
                .Returns(_groupRepositoryMock.Object);

            var virtualKey = new VirtualKey
            {
                Id = 123,
                KeyHash = "test-hash",
                VirtualKeyGroupId = 1,
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            };
            
            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 100m,
                LifetimeCreditsAdded = 100m,
                LifetimeSpent = 100m
            };

            _virtualKeyRepositoryMock
                .Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(virtualKey));
            
            _groupRepositoryMock
                .Setup(r => r.GetByIdAsync(1))
                .Returns(Task.FromResult(group));

            _groupRepositoryMock
                .Setup(r => r.AdjustBalanceAsync(
                    1,
                    -50m,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ReferenceType.VirtualKey,
                    It.IsAny<string>()))
                .Returns(Task.FromResult(50m)); // New balance

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 123,
                Amount = 50m,
                RequestId = "req-123",
                CorrelationId = "corr-123"
            };

            // Act
            await _processor.HandleAsync(@event, new TestEventContext());

            // Assert
            // Verify group balance was adjusted with correct reference type
            _groupRepositoryMock.Verify(r => r.AdjustBalanceAsync(
                1,
                -50m,
                "API usage by virtual key #123",
                "System",
                ReferenceType.VirtualKey,
                "123"), Times.Once);

            _eventBusMock.Verify(p => p.PublishAsync(It.Is<SpendUpdated>(su =>
                su.KeyId == 123 &&
                su.Amount == 50m &&
                su.NewTotalSpend == 150m &&
                su.RequestId == "req-123" &&
                su.CorrelationId == "corr-123"), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WithRepositoryUnavailable_PublishesDeferredEvent()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(null);
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyGroupRepository)))
                .Returns(null);

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 456,
                Amount = 75m,
                RequestId = "req-456",
                CorrelationId = "corr-456"
            };

            // Act
            await _processor.HandleAsync(@event, new TestEventContext());

            // Assert
            _eventBusMock.Verify(p => p.PublishAsync(It.Is<SpendUpdateDeferred>(sud =>
                sud.KeyId == 456 &&
                sud.Amount == 75m &&
                sud.RequestId == "req-456" &&
                sud.CorrelationId == "corr-456" &&
                sud.Reason == "Repository not available in current context"), 
                It.IsAny<CancellationToken>()), Times.Once);

            _virtualKeyRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WithZeroAmount_SkipsProcessing()
        {
            // Arrange
            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 789,
                Amount = 0m,
                RequestId = "req-789"
            };

            // Act
            await _processor.HandleAsync(@event, new TestEventContext());

            // Assert
            _serviceProviderMock.Verify(sp => sp.GetService(It.IsAny<Type>()), Times.Never);
            _eventBusMock.Verify(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WithNegativeAmount_SkipsProcessing()
        {
            // Arrange
            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 999,
                Amount = -10m,
                RequestId = "req-999"
            };

            // Act
            await _processor.HandleAsync(@event, new TestEventContext());

            // Assert
            _serviceProviderMock.Verify(sp => sp.GetService(It.IsAny<Type>()), Times.Never);
            _eventBusMock.Verify(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WithNonExistentVirtualKey_SkipsUpdate()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyGroupRepository)))
                .Returns(_groupRepositoryMock.Object);

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

            // Act
            await _processor.HandleAsync(@event, new TestEventContext());

            // Assert
            _virtualKeyRepositoryMock.Verify(r => r.GetByIdAsync(111, It.IsAny<CancellationToken>()), Times.Once);
            _groupRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
            _eventBusMock.Verify(p => p.PublishAsync(It.IsAny<SpendUpdated>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WithUpdateFailure_ThrowsException()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyGroupRepository)))
                .Returns(_groupRepositoryMock.Object);

            var virtualKey = new VirtualKey
            {
                Id = 222,
                KeyHash = "test-hash-222",
                VirtualKeyGroupId = 1
            };
            
            var group = new VirtualKeyGroup
            {
                Id = 1,
                GroupName = "Test Group",
                Balance = 100m
            };

            _virtualKeyRepositoryMock
                .Setup(r => r.GetByIdAsync(222, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(virtualKey));
            
            _groupRepositoryMock
                .Setup(r => r.GetByIdAsync(1))
                .Returns(Task.FromResult(group));

            // AdjustBalanceAsync returns negative balance indicating failure
            _groupRepositoryMock
                .Setup(r => r.AdjustBalanceAsync(
                    1,
                    -30m,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ReferenceType.VirtualKey,
                    It.IsAny<string>()))
                .Returns(Task.FromResult(-1m));

            var @event = new SpendUpdateRequested
            {
                EventId = Guid.NewGuid().ToString(),
                KeyId = 222,
                Amount = 30m,
                RequestId = "req-222"
            };

            // Act
            var act = () => _processor.HandleAsync(@event, new TestEventContext());

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Failed to update spend for virtual key group 1");
        }

        [Fact]
        public async Task HandleAsync_WithRepositoryException_ThrowsToTriggerRetry()
        {
            // Arrange
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyRepository)))
                .Returns(_virtualKeyRepositoryMock.Object);
            _serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IVirtualKeyGroupRepository)))
                .Returns(_groupRepositoryMock.Object);

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

            // Act
            var act = () => _processor.HandleAsync(@event, new TestEventContext());

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
                _eventBusMock.Object,
                logger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("serviceScopeFactory");
        }

        [Fact]
        public void Constructor_WithNullEventBus_ThrowsArgumentNullException()
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
                .WithParameterName("eventBus");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act
            var act = () => new SpendUpdateProcessor(
                _serviceScopeFactoryMock.Object,
                _eventBusMock.Object,
                null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }
    }
}