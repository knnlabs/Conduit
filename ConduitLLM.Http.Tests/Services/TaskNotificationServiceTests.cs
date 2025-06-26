using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Services;
using Polly.CircuitBreaker;

namespace ConduitLLM.Http.Tests.Services
{
    public class TaskNotificationServiceTests
    {
        private readonly Mock<IHubContext<TaskHub>> _taskHubContextMock;
        private readonly Mock<IHubContext<ImageGenerationHub>> _imageHubContextMock;
        private readonly Mock<IHubContext<VideoGenerationHub>> _videoHubContextMock;
        private readonly Mock<ILogger<TaskNotificationService>> _loggerMock;
        private readonly Mock<IHubClients> _hubClientsMock;
        private readonly Mock<IClientProxy> _clientProxyMock;
        private readonly TaskNotificationService _service;

        public TaskNotificationServiceTests()
        {
            _taskHubContextMock = new Mock<IHubContext<TaskHub>>();
            _imageHubContextMock = new Mock<IHubContext<ImageGenerationHub>>();
            _videoHubContextMock = new Mock<IHubContext<VideoGenerationHub>>();
            _loggerMock = new Mock<ILogger<TaskNotificationService>>();
            _hubClientsMock = new Mock<IHubClients>();
            _clientProxyMock = new Mock<IClientProxy>();

            _taskHubContextMock.Setup(x => x.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
            _hubClientsMock.Setup(x => x.All).Returns(_clientProxyMock.Object);

            _service = new TaskNotificationService(
                _taskHubContextMock.Object,
                _imageHubContextMock.Object,
                _videoHubContextMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task NotifyTaskStartedAsync_SendsToCorrectGroups()
        {
            // Arrange
            var taskId = "test-task-123";
            var taskType = "test_operation";
            var virtualKeyId = 456;
            var metadata = new Dictionary<string, object> { ["test"] = "value" };

            // Act
            await _service.NotifyTaskStartedAsync(taskId, taskType, virtualKeyId, metadata);

            // Assert
            _hubClientsMock.Verify(x => x.Group($"task-{taskId}"), Times.Once);
            _hubClientsMock.Verify(x => x.Group($"vkey-{virtualKeyId}-{taskType}"), Times.Once);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskStarted",
                It.Is<object[]>(args => args.Length == 3),
                default), Times.Exactly(2));
        }

        [Fact]
        public async Task NotifyTaskProgressAsync_SendsProgressUpdate()
        {
            // Arrange
            var taskId = "test-task-123";
            var progress = 75;
            var message = "Processing...";

            // Act
            await _service.NotifyTaskProgressAsync(taskId, progress, message);

            // Assert
            _hubClientsMock.Verify(x => x.Group($"task-{taskId}"), Times.Once);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskProgress",
                It.Is<object[]>(args => 
                    args.Length == 3 && 
                    args[0].ToString() == taskId &&
                    (int)args[1] == progress &&
                    args[2].ToString() == message),
                default), Times.Once);
        }

        [Fact]
        public async Task NotifyTaskFailedAsync_IncludesRetryableFlag()
        {
            // Arrange
            var taskId = "test-task-123";
            var error = "Test error";
            var isRetryable = true;

            // Act
            await _service.NotifyTaskFailedAsync(taskId, error, isRetryable);

            // Assert
            _hubClientsMock.Verify(x => x.Group($"task-{taskId}"), Times.Once);
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskFailed",
                It.Is<object[]>(args => 
                    args.Length == 3 && 
                    args[0].ToString() == taskId &&
                    args[1].ToString() == error &&
                    (bool)args[2] == isRetryable),
                default), Times.Once);
        }

        [Fact]
        public async Task CircuitBreaker_OpensAfterMultipleFailures()
        {
            // Arrange
            var taskId = "test-task-123";
            var taskType = "test_operation";
            var virtualKeyId = 456;

            // Make the hub throw exceptions
            _clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .ThrowsAsync(new Exception("Hub communication failed"));

            // Act - Trigger multiple failures to open circuit breaker
            for (int i = 0; i < 10; i++)
            {
                await _service.NotifyTaskStartedAsync($"{taskId}-{i}", taskType, virtualKeyId);
            }

            // Assert
            var circuitState = _service.GetCircuitState();
            Assert.Equal(CircuitState.Open, circuitState);
        }

        [Fact]
        public void GetHealthStatus_ReturnsCorrectStatus()
        {
            // Act
            var (isHealthy, state, lastFailure) = _service.GetHealthStatus();

            // Assert
            Assert.True(isHealthy);
            Assert.Equal("Closed", state);
            Assert.Null(lastFailure);
        }

        [Fact]
        public void ResetCircuitBreaker_ResetsTheCircuit()
        {
            // Act
            _service.ResetCircuitBreaker();

            // Assert
            var circuitState = _service.GetCircuitState();
            Assert.Equal(CircuitState.Closed, circuitState);
        }

        [Fact]
        public async Task RetryPolicy_RetriesOnTransientFailures()
        {
            // Arrange
            var taskId = "test-task-123";
            var progress = 50;
            var callCount = 0;

            _clientProxyMock
                .Setup(x => x.SendCoreAsync("TaskProgress", It.IsAny<object[]>(), default))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount < 3)
                    {
                        throw new Exception("Transient failure");
                    }
                    return Task.CompletedTask;
                });

            // Act
            await _service.NotifyTaskProgressAsync(taskId, progress, "Test");

            // Assert
            Assert.Equal(3, callCount); // Should retry twice before succeeding
        }

        [Fact]
        public async Task SendToLegacyHub_RoutesToCorrectHub()
        {
            // Arrange
            var taskId = "test-task-123";
            var taskType = "image_generation";
            var virtualKeyId = 456;
            
            var imageHubClients = new Mock<IHubClients>();
            var imageClientProxy = new Mock<IClientProxy>();
            
            _imageHubContextMock.Setup(x => x.Clients).Returns(imageHubClients.Object);
            imageHubClients.Setup(x => x.All).Returns(imageClientProxy.Object);
            imageHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(imageClientProxy.Object);

            // Act
            await _service.NotifyTaskStartedAsync(taskId, taskType, virtualKeyId);

            // Assert
            imageClientProxy.Verify(x => x.SendCoreAsync(
                "TaskStarted",
                It.IsAny<object[]>(),
                default), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ThreadSafety_HandlesMultipleConcurrentNotifications()
        {
            // Arrange
            var tasks = new List<Task>();
            var taskCount = 20;

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                var taskId = $"task-{i}";
                tasks.Add(Task.Run(async () =>
                {
                    await _service.NotifyTaskProgressAsync(taskId, i * 5, $"Progress {i}");
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskProgress",
                It.IsAny<object[]>(),
                default), Times.Exactly(taskCount));
        }

        [Fact]
        public async Task EnrichMetadata_AddsVirtualKeyId()
        {
            // Arrange
            var taskId = "test-task-123";
            var taskType = "test_operation";
            var virtualKeyId = 789;
            var metadata = new { customField = "value" };

            // Act
            await _service.NotifyTaskStartedAsync(taskId, taskType, virtualKeyId, metadata);

            // Assert
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskStarted",
                It.Is<object[]>(args => 
                    args.Length == 3 &&
                    args[2] != null &&
                    args[2].GetType() == typeof(Dictionary<string, object>) &&
                    ((Dictionary<string, object>)args[2]).ContainsKey("virtualKeyId") &&
                    (int)((Dictionary<string, object>)args[2])["virtualKeyId"] == virtualKeyId),
                default), Times.AtLeastOnce);
        }

        [Fact]
        public async Task LegacyHubFailure_DoesNotBreakMainNotification()
        {
            // Arrange
            var taskId = "test-task-123";
            var taskType = "image_generation";
            var virtualKeyId = 456;

            // Make legacy hub throw exception
            var imageHubClients = new Mock<IHubClients>();
            var imageClientProxy = new Mock<IClientProxy>();
            
            _imageHubContextMock.Setup(x => x.Clients).Returns(imageHubClients.Object);
            imageHubClients.Setup(x => x.All).Returns(imageClientProxy.Object);
            imageClientProxy
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .ThrowsAsync(new Exception("Legacy hub failed"));

            // Act & Assert - Should not throw
            await _service.NotifyTaskStartedAsync(taskId, taskType, virtualKeyId);

            // Main hub should still be called
            _clientProxyMock.Verify(x => x.SendCoreAsync(
                "TaskStarted",
                It.IsAny<object[]>(),
                default), Times.AtLeastOnce);
        }
    }
}