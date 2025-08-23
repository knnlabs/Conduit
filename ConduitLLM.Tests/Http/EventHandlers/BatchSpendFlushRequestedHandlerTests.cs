using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Http.EventHandlers;

namespace ConduitLLM.Tests.Http.EventHandlers
{
    /// <summary>
    /// Unit tests for BatchSpendFlushRequestedHandler.
    /// Tests event consumption, service interaction, error handling, and completion event publishing.
    /// Note: These tests focus on the handler logic rather than the BatchSpendUpdateService implementation.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "BatchSpendFlushRequestedHandler")]
    public class BatchSpendFlushRequestedHandlerTests
    {
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
        private readonly Mock<IServiceScope> _serviceScopeMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly Mock<ILogger<BatchSpendFlushRequestedHandler>> _loggerMock;
        private readonly Mock<ConsumeContext<BatchSpendFlushRequestedEvent>> _contextMock;
        private readonly BatchSpendFlushRequestedHandler _handler;

        public BatchSpendFlushRequestedHandlerTests()
        {
            _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
            _serviceScopeMock = new Mock<IServiceScope>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _loggerMock = new Mock<ILogger<BatchSpendFlushRequestedHandler>>();
            _contextMock = new Mock<ConsumeContext<BatchSpendFlushRequestedEvent>>();

            // Setup service scope chain
            _serviceScopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
            _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);

            _handler = new BatchSpendFlushRequestedHandler(
                _serviceScopeFactoryMock.Object,
                _publishEndpointMock.Object,
                _loggerMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(_handler);
        }

        [Fact]
        public void Constructor_WithNullServiceScopeFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new BatchSpendFlushRequestedHandler(null!, _publishEndpointMock.Object, _loggerMock.Object));

            Assert.Equal("serviceScopeFactory", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullPublishEndpoint_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new BatchSpendFlushRequestedHandler(_serviceScopeFactoryMock.Object, null!, _loggerMock.Object));

            Assert.Equal("publishEndpoint", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new BatchSpendFlushRequestedHandler(_serviceScopeFactoryMock.Object, _publishEndpointMock.Object, null!));

            Assert.Equal("logger", exception.ParamName);
        }

        #endregion

        #region Service Unavailable Tests

        [Fact]
        public async Task Consume_WhenBatchServiceNotAvailable_PublishesFailureEvent()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var flushRequest = new BatchSpendFlushRequestedEvent { RequestId = requestId };

            _serviceProviderMock.Setup(x => x.GetService(typeof(IBatchSpendUpdateService)))
                .Returns((IBatchSpendUpdateService?)null);

            _contextMock.Setup(x => x.Message).Returns(flushRequest);
            _contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            BatchSpendFlushCompletedEvent? publishedEvent = null;
            _publishEndpointMock.Setup(x => x.Publish(It.IsAny<BatchSpendFlushCompletedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BatchSpendFlushCompletedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _handler.Consume(_contextMock.Object);

            // Assert
            Assert.NotNull(publishedEvent);
            Assert.Equal(requestId, publishedEvent.RequestId);
            Assert.False(publishedEvent.Success);
            Assert.Equal(0, publishedEvent.GroupsFlushed);
            Assert.Contains("BatchSpendUpdateService not available", publishedEvent.ErrorMessage);
        }

        [Fact]
        public async Task Consume_WhenServiceIsWrongType_PublishesFailureEvent()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var flushRequest = new BatchSpendFlushRequestedEvent { RequestId = requestId };

            var wrongServiceMock = new Mock<IBatchSpendUpdateService>();
            _serviceProviderMock.Setup(x => x.GetService(typeof(IBatchSpendUpdateService)))
                .Returns(wrongServiceMock.Object);

            _contextMock.Setup(x => x.Message).Returns(flushRequest);
            _contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            BatchSpendFlushCompletedEvent? publishedEvent = null;
            _publishEndpointMock.Setup(x => x.Publish(It.IsAny<BatchSpendFlushCompletedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BatchSpendFlushCompletedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _handler.Consume(_contextMock.Object);

            // Assert
            Assert.NotNull(publishedEvent);
            Assert.Equal(requestId, publishedEvent.RequestId);
            Assert.False(publishedEvent.Success);
            Assert.Contains("not the expected concrete implementation", publishedEvent.ErrorMessage);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Consume_WhenPublishFails_DoesNotThrowException()
        {
            // Arrange
            var flushRequest = new BatchSpendFlushRequestedEvent { RequestId = Guid.NewGuid().ToString() };

            _serviceProviderMock.Setup(x => x.GetService(typeof(IBatchSpendUpdateService)))
                .Returns((IBatchSpendUpdateService?)null);

            _contextMock.Setup(x => x.Message).Returns(flushRequest);
            _contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            _publishEndpointMock.Setup(x => x.Publish(It.IsAny<BatchSpendFlushCompletedEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Message bus unavailable"));

            // Act & Assert
            await _handler.Consume(_contextMock.Object); // Should not throw
        }

        #endregion

        #region Event Structure Tests

        [Fact]
        public async Task Consume_Always_PublishesCompletionEventWithCorrectStructure()
        {
            // Arrange
            var requestId = Guid.NewGuid().ToString();
            var flushRequest = new BatchSpendFlushRequestedEvent 
            { 
                RequestId = requestId,
                RequestedBy = "TestUser",
                Source = "Unit Test"
            };

            _serviceProviderMock.Setup(x => x.GetService(typeof(IBatchSpendUpdateService)))
                .Returns((IBatchSpendUpdateService?)null);

            _contextMock.Setup(x => x.Message).Returns(flushRequest);
            _contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            BatchSpendFlushCompletedEvent? publishedEvent = null;
            _publishEndpointMock.Setup(x => x.Publish(It.IsAny<BatchSpendFlushCompletedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BatchSpendFlushCompletedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _handler.Consume(_contextMock.Object);

            // Assert - Verify event structure
            Assert.NotNull(publishedEvent);
            Assert.Equal(requestId, publishedEvent.RequestId);
            Assert.True(publishedEvent.CompletedAt <= DateTime.UtcNow);
            Assert.True(publishedEvent.CompletedAt >= DateTime.UtcNow.AddSeconds(-5));
            Assert.True(publishedEvent.Duration >= TimeSpan.Zero);
            
            // Verify audit trail information is preserved
            Assert.NotNull(publishedEvent.ErrorMessage);
        }

        [Fact]
        public async Task Consume_WithDifferentRequestProperties_PreservesRequestId()
        {
            // Arrange
            var testCases = new[]
            {
                new BatchSpendFlushRequestedEvent { RequestId = "test-1", Priority = FlushPriority.Normal },
                new BatchSpendFlushRequestedEvent { RequestId = "test-2", Priority = FlushPriority.High },
                new BatchSpendFlushRequestedEvent { RequestId = "test-3", TimeoutSeconds = 30 }
            };

            _serviceProviderMock.Setup(x => x.GetService(typeof(IBatchSpendUpdateService)))
                .Returns((IBatchSpendUpdateService?)null);

            _contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            foreach (var testCase in testCases)
            {
                // Arrange for this test case
                _contextMock.Setup(x => x.Message).Returns(testCase);

                BatchSpendFlushCompletedEvent? publishedEvent = null;
                _publishEndpointMock.Setup(x => x.Publish(It.IsAny<BatchSpendFlushCompletedEvent>(), It.IsAny<CancellationToken>()))
                    .Callback<BatchSpendFlushCompletedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                    .Returns(Task.CompletedTask);

                // Act
                await _handler.Consume(_contextMock.Object);

                // Assert
                Assert.NotNull(publishedEvent);
                Assert.Equal(testCase.RequestId, publishedEvent.RequestId);
            }
        }

        #endregion

        #region Service Scope Tests

        [Fact]
        public async Task Consume_Always_CreatesServiceScope()
        {
            // Arrange
            var flushRequest = new BatchSpendFlushRequestedEvent { RequestId = Guid.NewGuid().ToString() };

            _serviceProviderMock.Setup(x => x.GetService(typeof(IBatchSpendUpdateService)))
                .Returns((IBatchSpendUpdateService?)null);

            _contextMock.Setup(x => x.Message).Returns(flushRequest);
            _contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            _publishEndpointMock.Setup(x => x.Publish(It.IsAny<BatchSpendFlushCompletedEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.Consume(_contextMock.Object);

            // Assert
            _serviceScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
            _serviceScopeMock.Verify(x => x.ServiceProvider, Times.AtLeastOnce);
        }

        #endregion
    }
}