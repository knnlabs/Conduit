using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Filters;
using ConduitLLM.Http.Metrics;
using System.Security.Claims;

namespace ConduitLLM.Http.Tests.Filters
{
    public class SignalRMetricsFilterTests
    {
        private readonly Mock<ILogger<SignalRMetricsFilter>> _loggerMock;
        private readonly Mock<SignalRMetrics> _metricsMock;
        private readonly SignalRMetricsFilter _filter;

        public SignalRMetricsFilterTests()
        {
            _loggerMock = new Mock<ILogger<SignalRMetricsFilter>>();
            _metricsMock = new Mock<SignalRMetrics>();
            _filter = new SignalRMetricsFilter(_loggerMock.Object, _metricsMock.Object);
        }

        [Fact]
        public async Task OnConnectedAsync_LogsConnectionAndIncreasesMetrics()
        {
            // Arrange
            var hubType = typeof(TestHub);
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var context = CreateHubLifetimeContext(hub, connectionId);

            // Act
            await _filter.OnConnectedAsync(context, _ => Task.CompletedTask);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SignalR connection established")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_WithException_LogsErrorAndUpdatesMetrics()
        {
            // Arrange
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var context = CreateHubLifetimeContext(hub, connectionId);
            var exception = new Exception("Connection error");

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _filter.OnConnectedAsync(context, _ => throw exception));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during SignalR connection")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_LogsDisconnection()
        {
            // Arrange
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var context = CreateHubLifetimeContext(hub, connectionId);

            // Act
            await _filter.OnDisconnectedAsync(context, null, (ctx, ex) => Task.CompletedTask);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SignalR connection disconnected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_WithException_LogsWarning()
        {
            // Arrange
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var context = CreateHubLifetimeContext(hub, connectionId);
            var exception = new Exception("Disconnect error");

            // Act
            await _filter.OnDisconnectedAsync(context, exception, (ctx, ex) => Task.CompletedTask);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SignalR connection disconnected with error")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task InvokeMethodAsync_LogsMethodInvocation()
        {
            // Arrange
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var methodName = "TestMethod";
            var invocationContext = CreateHubInvocationContext(hub, connectionId, methodName);

            _metricsMock.Setup(x => x.RecordHubMethodInvocation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
                .Returns(Mock.Of<IDisposable>());

            // Act
            var result = await _filter.InvokeMethodAsync(invocationContext, _ => new ValueTask<object?>("result"));

            // Assert
            Assert.Equal("result", result);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Invoking SignalR hub method {methodName}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task InvokeMethodAsync_WithHubException_LogsWarning()
        {
            // Arrange
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var methodName = "TestMethod";
            var invocationContext = CreateHubInvocationContext(hub, connectionId, methodName);
            var hubException = new HubException("Hub error");

            _metricsMock.Setup(x => x.RecordHubMethodInvocation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
                .Returns(Mock.Of<IDisposable>());

            // Act & Assert
            await Assert.ThrowsAsync<HubException>(() =>
                _filter.InvokeMethodAsync(invocationContext, _ => throw hubException));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Hub exception in method")),
                hubException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task InvokeMethodAsync_WithException_LogsError()
        {
            // Arrange
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var methodName = "TestMethod";
            var invocationContext = CreateHubInvocationContext(hub, connectionId, methodName);
            var exception = new Exception("Method error");

            _metricsMock.Setup(x => x.RecordHubMethodInvocation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
                .Returns(Mock.Of<IDisposable>());

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _filter.InvokeMethodAsync(invocationContext, _ => throw exception));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error invoking SignalR hub method")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task InvokeMethodAsync_ExtractsVirtualKeyIdFromItems()
        {
            // Arrange
            var hub = new TestHub();
            var connectionId = "test-connection-id";
            var methodName = "TestMethod";
            var virtualKeyId = 123;
            
            var hubCallerContext = new Mock<HubCallerContext>();
            hubCallerContext.Setup(x => x.ConnectionId).Returns(connectionId);
            hubCallerContext.Setup(x => x.Items).Returns(new Dictionary<object, object?> { ["VirtualKeyId"] = virtualKeyId });

            var invocationContext = new HubInvocationContext(hubCallerContext.Object, "services", hub, methodName, Array.Empty<object>());

            _metricsMock.Setup(x => x.RecordHubMethodInvocation("TestHub", methodName, virtualKeyId))
                .Returns(Mock.Of<IDisposable>());

            // Act
            await _filter.InvokeMethodAsync(invocationContext, _ => new ValueTask<object?>("result"));

            // Assert
            _metricsMock.Verify(x => x.RecordHubMethodInvocation("TestHub", methodName, virtualKeyId), Times.Once);
        }

        private HubLifetimeContext CreateHubLifetimeContext(Hub hub, string connectionId)
        {
            var hubCallerContext = new Mock<HubCallerContext>();
            hubCallerContext.Setup(x => x.ConnectionId).Returns(connectionId);
            hubCallerContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>());
            
            var hubContext = new HubLifetimeContext(hubCallerContext.Object, null!, hub);
            return hubContext;
        }

        private HubInvocationContext CreateHubInvocationContext(Hub hub, string connectionId, string methodName)
        {
            var hubCallerContext = new Mock<HubCallerContext>();
            hubCallerContext.Setup(x => x.ConnectionId).Returns(connectionId);
            hubCallerContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>());
            
            return new HubInvocationContext(hubCallerContext.Object, "services", hub, methodName, Array.Empty<object>());
        }

        private class TestHub : Hub
        {
            public string TestMethod() => "test";
        }
    }
}